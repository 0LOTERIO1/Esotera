using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Hidratação manual de J3OrderCode/J3TrackingNumber via getOrderDetails.
/// Não altera J3Fulfillment.Status (integração). Zero mutations J3.
/// Não persiste J3RemoteStatus (responsabilidade do TRACK-1).
/// </summary>
public sealed class J3IdentifierHydrationService : IJ3IdentifierHydrationService
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3OrderDetailsClient _details;
    private readonly ILogger<J3IdentifierHydrationService> _logger;

    public J3IdentifierHydrationService(
        EsoteraDbContext context,
        IJ3OrderDetailsClient details,
        ILogger<J3IdentifierHydrationService> logger)
    {
        _context = context;
        _details = details;
        _logger = logger;
    }

    public async Task<J3IdentifierHydrationOutcome> HydrateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return J3IdentifierHydrationOutcome.NotFound();

        var fulfillment = await _context.J3Fulfillments
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
        if (fulfillment is null)
        {
            return J3IdentifierHydrationOutcome.Conflict(
                J3IdentifierHydrationErrorCodes.NotEligible,
                "J3Fulfillment não encontrado para o pedido.");
        }

        if (!string.Equals(fulfillment.Status, J3FulfillmentStatus.Created, StringComparison.Ordinal))
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3IdentifierHydrationErrorCodes.NotEligible,
                "Hidratação exige fulfillment created.");
        }

        if (string.IsNullOrWhiteSpace(fulfillment.J3OrderId))
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3IdentifierHydrationErrorCodes.NotEligible,
                "J3OrderId ausente — hidratação não elegível.");
        }

        var hasCode = !string.IsNullOrWhiteSpace(fulfillment.J3OrderCode);
        var hasTracking = !string.IsNullOrWhiteSpace(fulfillment.J3TrackingNumber);

        if (hasCode ^ hasTracking)
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3IdentifierHydrationErrorCodes.LocalConflict,
                "J3OrderCode e J3TrackingNumber parcialmente preenchidos.");
        }

        if (hasCode && hasTracking)
        {
            if (!J3ReconcileMatcher.CodesEqual(fulfillment.J3OrderCode, fulfillment.J3TrackingNumber))
            {
                return ConflictNoLookup(
                    order,
                    fulfillment,
                    J3IdentifierHydrationErrorCodes.LocalConflict,
                    "J3OrderCode e J3TrackingNumber locais divergem.");
            }

            return J3IdentifierHydrationOutcome.Ok(
                BuildBody(
                    order,
                    fulfillment,
                    outcome: "AlreadyHydrated",
                    error: null,
                    lookupSent: false));
        }

        var remoteOrderId = fulfillment.J3OrderId.Trim();
        var lookup = await _details.GetByOrderIdAsync(remoteOrderId, cancellationToken);

        if (lookup.Outcome == J3OrderDetailsLookupOutcome.Failed)
        {
            var err = MapLookupFailureCode(lookup.ErrorCode);
            return Unprocessable(
                order,
                fulfillment,
                err,
                "Falha no lookup read-only getOrderDetails.",
                lookupSent: true);
        }

        if (lookup.Outcome == J3OrderDetailsLookupOutcome.NotFound || lookup.Response is null)
        {
            return Unprocessable(
                order,
                fulfillment,
                J3IdentifierHydrationErrorCodes.NotFound,
                "Pedido J3 não encontrado pelo orderId.",
                lookupSent: true);
        }

        // Status remoto: apenas log sanitizado — não persiste (TRACK-1).
        if (!string.IsNullOrWhiteSpace(lookup.Response.Status))
        {
            _logger.LogInformation(
                "J3 identifier hydration saw remote status order {OrderId} fulfillment {FulfillmentId} remoteStatusPresent {Present}",
                order.Id,
                fulfillment.Id,
                true);
        }

        var (tracking, matchError) = J3IdentifierHydrationIdentity.TryValidate(
            order,
            fulfillment,
            lookup.Response);
        if (matchError is not null || tracking is null)
        {
            var codeErr = J3FulfillmentErrorCodes.Sanitize(matchError)
                ?? J3IdentifierHydrationErrorCodes.LookupFailed;
            _logger.LogWarning(
                "J3 identifier hydration identity fail order {OrderId} fulfillment {FulfillmentId} error {ErrorCode}",
                order.Id,
                fulfillment.Id,
                codeErr);
            return Unprocessable(
                order,
                fulfillment,
                codeErr,
                "Identidade do pedido J3 divergente ou tracking ausente.",
                lookupSent: true);
        }

        var now = DateTime.UtcNow;
        fulfillment.J3OrderCode = tracking;
        fulfillment.J3TrackingNumber = tracking;
        fulfillment.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "J3 identifier hydration succeeded order {OrderId} fulfillment {FulfillmentId} j3OrderId {J3OrderId} trackingPresent {TrackingPresent}",
            order.Id,
            fulfillment.Id,
            remoteOrderId,
            true);

        return J3IdentifierHydrationOutcome.Ok(
            BuildBody(order, fulfillment, outcome: "Success", error: null, lookupSent: true));
    }

    private static string MapLookupFailureCode(string? raw)
    {
        var sanitized = J3FulfillmentErrorCodes.Sanitize(raw)
            ?? J3IdentifierHydrationErrorCodes.LookupFailed;
        if (string.Equals(sanitized, J3FulfillmentErrorCodes.Configuration, StringComparison.Ordinal)
            || string.Equals(sanitized, J3FulfillmentErrorCodes.AuthLoginFailed, StringComparison.Ordinal))
        {
            return J3IdentifierHydrationErrorCodes.LookupFailed;
        }

        return sanitized;
    }

    private static J3IdentifierHydrationOutcome ConflictNoLookup(
        Order order,
        J3Fulfillment fulfillment,
        string reasonCode,
        string message) =>
        J3IdentifierHydrationOutcome.Conflict(
            reasonCode,
            message,
            BuildBody(order, fulfillment, outcome: "Conflict", error: reasonCode, lookupSent: false));

    private static J3IdentifierHydrationOutcome Unprocessable(
        Order order,
        J3Fulfillment fulfillment,
        string reasonCode,
        string message,
        bool lookupSent) =>
        J3IdentifierHydrationOutcome.Unprocessable(
            reasonCode,
            message,
            BuildBody(order, fulfillment, outcome: "Failed", error: reasonCode, lookupSent: lookupSent));

    private static J3IdentifierHydrationResultDto BuildBody(
        Order order,
        J3Fulfillment fulfillment,
        string outcome,
        string? error,
        bool lookupSent) =>
        new(
            order.Id,
            order.OrderNumber,
            fulfillment.Id,
            fulfillment.Status,
            fulfillment.J3OrderId,
            fulfillment.J3OrderCode,
            fulfillment.J3TrackingNumber,
            outcome,
            error,
            lookupSent,
            J3GetOrderDetailsQuery.OperationName);
}
