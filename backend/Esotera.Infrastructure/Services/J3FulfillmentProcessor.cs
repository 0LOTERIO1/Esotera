using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Processor J3: Pending → claim → (RetryableFailure | Created | UnknownOutcome).
/// Gate: J3_FULFILLMENT_ENABLED. Não exige J3_ENABLED (pedidos históricos já pagos).
/// Sem stamp, sem retry, sem BackgroundService.
/// </summary>
public sealed class J3FulfillmentProcessor : IJ3FulfillmentProcessor
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3FulfillmentService _fulfillment;
    private readonly IJ3FulfillmentClient _client;
    private readonly J3ShippingOptions _j3;
    private readonly ILogger<J3FulfillmentProcessor> _logger;

    public J3FulfillmentProcessor(
        EsoteraDbContext context,
        IJ3FulfillmentService fulfillment,
        IJ3FulfillmentClient client,
        IOptions<J3ShippingOptions> j3Options,
        ILogger<J3FulfillmentProcessor> logger)
    {
        _context = context;
        _fulfillment = fulfillment;
        _client = client;
        _j3 = j3Options.Value;
        _logger = logger;
    }

    public async Task<J3FulfillmentAdminDto?> GetSnapshotAsync(
        Guid fulfillmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.J3Fulfillments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (row is null)
            return null;

        return new J3FulfillmentAdminDto(
            row.Id,
            row.OrderId,
            row.Status,
            row.J3OrderId,
            row.J3OrderCode,
            row.J3TrackingNumber,
            row.J3DeliveryPointId,
            row.AttemptCount,
            row.LastErrorCode,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);
    }

    public async Task ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default)
    {
        if (!_j3.FulfillmentEnabled)
        {
            _logger.LogInformation(
                "J3 processor skipped fulfillment {FulfillmentId}: fulfillment flag disabled (no claim, no HTTP).",
                fulfillmentId);
            return;
        }

        var current = await _context.J3Fulfillments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (current is null)
            return;

        if (current.Status != J3FulfillmentStatus.Pending)
        {
            _logger.LogInformation(
                "J3 processor skipped fulfillment {FulfillmentId} status {Status} (no client).",
                fulfillmentId,
                current.Status);
            return;
        }

        var orderPreview = await _context.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == current.OrderId, cancellationToken);
        if (!IsEligibleOrder(orderPreview))
        {
            _logger.LogInformation(
                "J3 processor skipped fulfillment {FulfillmentId} order {OrderId}: not eligible (no claim, no HTTP).",
                fulfillmentId,
                current.OrderId);
            return;
        }

        var claimed = await _fulfillment.TryClaimPendingAsync(fulfillmentId, cancellationToken);
        if (!claimed)
        {
            _logger.LogInformation(
                "J3 processor lost claim for fulfillment {FulfillmentId} (no client).",
                fulfillmentId);
            return;
        }

        var order = await _context.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == current.OrderId, cancellationToken);
        if (!IsEligibleOrder(order))
        {
            await MarkRetryableFailureAsync(fulfillmentId, J3FulfillmentErrorCodes.Configuration, cancellationToken);
            return;
        }

        var settings = await _context.StoreSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? StoreSettingsService.CreateDefault();

        if (order!.ShippingIsResidentialAddress is null)
        {
            await MarkRetryableFailureAsync(
                fulfillmentId,
                J3FulfillmentErrorCodes.ResidentialRequired,
                cancellationToken);
            return;
        }

        var built = J3CreateTmsOrderMapper.TryBuild(order, settings, _j3);
        if (!built.IsValid)
        {
            await MarkRetryableFailureAsync(
                fulfillmentId,
                built.ErrorCode ?? J3FulfillmentErrorCodes.Configuration,
                cancellationToken);
            return;
        }

        var attempt = await _client.CreateOrderAsync(order, settings, cancellationToken);
        switch (attempt.Outcome)
        {
            case J3CreateOrderOutcome.Success:
                await MarkCreatedAsync(fulfillmentId, attempt, cancellationToken);
                break;
            case J3CreateOrderOutcome.DefiniteFailure:
                await MarkRetryableFailureAsync(
                    fulfillmentId,
                    attempt.ErrorCode ?? J3FulfillmentErrorCodes.Configuration,
                    cancellationToken);
                break;
            default:
                await MarkUnknownOutcomeAsync(
                    fulfillmentId,
                    attempt.ErrorCode ?? J3FulfillmentErrorCodes.Unknown,
                    cancellationToken);
                break;
        }
    }

    private static bool IsEligibleOrder(Order? order) =>
        order is not null
        && order.Status == OrderStatus.PaymentApproved
        && string.Equals(order.ShippingMethodId, ShippingMethod.J3, StringComparison.OrdinalIgnoreCase);

    private async Task MarkRetryableFailureAsync(
        Guid fulfillmentId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var code = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Configuration;
        await ApplyStatusAsync(
            fulfillmentId,
            J3FulfillmentStatus.RetryableFailure,
            now,
            row =>
            {
                row.LastErrorCode = code;
                row.LastErrorAtUtc = now;
            },
            cancellationToken);
        _logger.LogInformation(
            "J3 processor fulfillment {FulfillmentId} outcome {Outcome} error {ErrorCode}",
            fulfillmentId,
            J3FulfillmentStatus.RetryableFailure,
            code);
    }

    private async Task MarkUnknownOutcomeAsync(
        Guid fulfillmentId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var code = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Unknown;
        await ApplyStatusAsync(
            fulfillmentId,
            J3FulfillmentStatus.UnknownOutcome,
            now,
            row =>
            {
                row.LastErrorCode = code;
                row.LastErrorAtUtc = now;
            },
            cancellationToken);
        _logger.LogWarning(
            "J3 processor fulfillment {FulfillmentId} outcome {Outcome} error {ErrorCode} (terminal automatic; no retry)",
            fulfillmentId,
            J3FulfillmentStatus.UnknownOutcome,
            code);
    }

    private async Task MarkCreatedAsync(
        Guid fulfillmentId,
        J3CreateOrderAttemptResult attempt,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        try
        {
            await ApplyStatusAsync(
                fulfillmentId,
                J3FulfillmentStatus.Created,
                now,
                row =>
                {
                    row.J3OrderId = attempt.OrderId;
                    row.J3OrderCode = attempt.OrderCode;
                    row.J3TrackingNumber = attempt.TrackingNumber;
                    row.J3DeliveryPointId = attempt.DeliveryPointId;
                    row.CompletedAtUtc = now;
                    row.LastErrorCode = null;
                    row.LastErrorAtUtc = null;
                },
                cancellationToken);
            _logger.LogInformation(
                "J3 processor fulfillment {FulfillmentId} outcome {Outcome} (stamp not generated)",
                fulfillmentId,
                J3FulfillmentStatus.Created);
        }
        catch (Exception ex)
        {
            // IDs J3 existem remotamente; segunda mutation é proibida. Status local pode permanecer Processing.
            _logger.LogCritical(
                ex,
                "J3 processor CRITICAL: remote Success for fulfillment {FulfillmentId} but local persist failed. Do not retry mutation.",
                fulfillmentId);
        }
    }

    private async Task ApplyStatusAsync(
        Guid fulfillmentId,
        string status,
        DateTime now,
        Action<J3Fulfillment> mutate,
        CancellationToken cancellationToken)
    {
        var row = await _context.J3Fulfillments
            .FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (row is null)
            return;

        row.Status = status;
        row.UpdatedAtUtc = now;
        mutate(row);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
