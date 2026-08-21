using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Ação Admin manual J3. Não chama client HTTP diretamente — só o processor.
/// </summary>
public sealed class J3FulfillmentAdminProcessService : IJ3FulfillmentAdminProcessService
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3FulfillmentEligibilityService _eligibility;
    private readonly IJ3FulfillmentService _fulfillment;
    private readonly IJ3FulfillmentProcessor _processor;
    private readonly IJ3FulfillmentAdminQueryService _queries;
    private readonly J3ShippingOptions _j3;
    private readonly ILogger<J3FulfillmentAdminProcessService> _logger;

    public J3FulfillmentAdminProcessService(
        EsoteraDbContext context,
        IJ3FulfillmentEligibilityService eligibility,
        IJ3FulfillmentService fulfillment,
        IJ3FulfillmentProcessor processor,
        IJ3FulfillmentAdminQueryService queries,
        IOptions<J3ShippingOptions> j3Options,
        ILogger<J3FulfillmentAdminProcessService> logger)
    {
        _context = context;
        _eligibility = eligibility;
        _fulfillment = fulfillment;
        _processor = processor;
        _queries = queries;
        _j3 = j3Options.Value;
        _logger = logger;
    }

    public async Task<J3FulfillmentAdminProcessOutcome> ProcessOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var orderExists = await _context.Orders.AsNoTracking()
            .AnyAsync(o => o.Id == orderId, cancellationToken);
        if (!orderExists)
            return J3FulfillmentAdminProcessOutcome.NotFound();

        if (!_j3.FulfillmentEnabled)
        {
            _logger.LogInformation(
                "J3 admin process skipped order {OrderId}: FeatureDisabled (no EnsurePending, no claim, no HTTP).",
                orderId);
            var disabledSnap = await BuildDtoAsync(orderId, processed: false, cancellationToken);
            return J3FulfillmentAdminProcessOutcome.Conflict(
                J3FulfillmentEligibilityCodes.FeatureDisabled,
                "Integração J3 está desabilitada.",
                disabledSnap);
        }

        var existing = await _context.J3Fulfillments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case J3FulfillmentStatus.Created:
                    _logger.LogInformation(
                        "J3 admin process order {OrderId} fulfillment {FulfillmentId}: already Created (no HTTP).",
                        orderId,
                        existing.Id);
                    return J3FulfillmentAdminProcessOutcome.Ok(
                        (await BuildDtoAsync(orderId, processed: false, cancellationToken))!);

                case J3FulfillmentStatus.Processing:
                    return J3FulfillmentAdminProcessOutcome.Conflict(
                        J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists,
                        "Fulfillment J3 já está em processamento.",
                        await BuildDtoAsync(orderId, processed: false, cancellationToken));

                case J3FulfillmentStatus.UnknownOutcome:
                    return J3FulfillmentAdminProcessOutcome.Conflict(
                        J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview,
                        "Resultado incerto. Não reenviar automaticamente.",
                        await BuildDtoAsync(orderId, processed: false, cancellationToken));

                case J3FulfillmentStatus.RetryableFailure:
                    return J3FulfillmentAdminProcessOutcome.Conflict(
                        J3FulfillmentEligibilityCodes.RetryableFailureNotAutoRetried,
                        "Falha anterior exige revisão; retry automático não disponível nesta fase.",
                        await BuildDtoAsync(orderId, processed: false, cancellationToken));
            }
        }

        // Avaliar com flag ON (já checada) — regras Order/fiscal/endereço/Pending.
        var eligibility = await _eligibility.EvaluateForOrderAsync(orderId, cancellationToken);
        if (!eligibility.IsEligible)
        {
            _logger.LogInformation(
                "J3 admin process rejected order {OrderId}: {ReasonCode} (no EnsurePending, no HTTP).",
                orderId,
                eligibility.ReasonCode);
            return J3FulfillmentAdminProcessOutcome.Conflict(
                eligibility.ReasonCode,
                MapUserMessage(eligibility.ReasonCode, eligibility.Message),
                await BuildDtoAsync(orderId, processed: false, cancellationToken));
        }

        if (existing is null)
        {
            await _fulfillment.EnsurePendingAsync(orderId, cancellationToken);
            existing = await _context.J3Fulfillments.AsNoTracking()
                .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
            if (existing is null)
            {
                _logger.LogWarning(
                    "J3 admin process order {OrderId}: EnsurePending did not create row.",
                    orderId);
                return J3FulfillmentAdminProcessOutcome.Conflict(
                    J3FulfillmentErrorCodes.Configuration,
                    "Não foi possível preparar o fulfillment J3.",
                    await BuildDtoAsync(orderId, processed: false, cancellationToken));
            }
        }

        if (existing.Status != J3FulfillmentStatus.Pending)
        {
            // Corrida: outro request avançou o status.
            if (existing.Status == J3FulfillmentStatus.Created)
            {
                return J3FulfillmentAdminProcessOutcome.Ok(
                    (await BuildDtoAsync(orderId, processed: false, cancellationToken))!);
            }

            return J3FulfillmentAdminProcessOutcome.Conflict(
                MapStatusReason(existing.Status),
                MapUserMessage(MapStatusReason(existing.Status), "Estado J3 não processável."),
                await BuildDtoAsync(orderId, processed: false, cancellationToken));
        }

        _logger.LogInformation(
            "J3 admin process starting order {OrderId} fulfillment {FulfillmentId}.",
            orderId,
            existing.Id);

        await _processor.ProcessAsync(existing.Id, cancellationToken);

        var after = await BuildDtoAsync(orderId, processed: true, cancellationToken);
        _logger.LogInformation(
            "J3 admin process finished order {OrderId} fulfillment {FulfillmentId} status {Status}.",
            orderId,
            after?.FulfillmentId,
            after?.Status);

        return J3FulfillmentAdminProcessOutcome.Ok(after!);
    }

    private async Task<J3FulfillmentAdminProcessDto?> BuildDtoAsync(
        Guid orderId,
        bool processed,
        CancellationToken cancellationToken)
    {
        var orderNumber = await _context.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var row = await _context.J3Fulfillments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);

        var eligibility = await _eligibility.EvaluateForOrderAsync(orderId, cancellationToken);

        if (row is null)
        {
            return new J3FulfillmentAdminProcessDto(
                orderId,
                null,
                orderNumber,
                "none",
                eligibility.IsEligible,
                eligibility.ReasonCode,
                null,
                null,
                null,
                0,
                null,
                null,
                false,
                processed);
        }

        var detail = await _queries.GetAsync(row.Id, cancellationToken);
        if (detail is null)
        {
            return new J3FulfillmentAdminProcessDto(
                orderId,
                row.Id,
                orderNumber,
                row.Status,
                eligibility.IsEligible,
                eligibility.ReasonCode,
                row.J3OrderId,
                row.J3OrderCode,
                row.J3TrackingNumber,
                row.AttemptCount,
                row.CreatedAtUtc,
                row.UpdatedAtUtc,
                J3FulfillmentAdminFlags.NeedsManualReview(row.Status, false),
                processed);
        }

        return new J3FulfillmentAdminProcessDto(
            detail.OrderId,
            detail.Id,
            detail.OrderNumber,
            detail.Status,
            detail.CanSendToJ3,
            detail.EligibilityReason,
            detail.J3OrderId,
            detail.J3OrderCode,
            detail.J3TrackingNumber,
            detail.AttemptCount,
            detail.CreatedAtUtc,
            detail.UpdatedAtUtc,
            detail.NeedsManualReview,
            processed);
    }

    private static string MapStatusReason(string status) =>
        status switch
        {
            J3FulfillmentStatus.Processing => J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists,
            J3FulfillmentStatus.Created => J3FulfillmentEligibilityCodes.FulfillmentAlreadyCreated,
            J3FulfillmentStatus.UnknownOutcome => J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview,
            J3FulfillmentStatus.RetryableFailure => J3FulfillmentEligibilityCodes.RetryableFailureNotAutoRetried,
            _ => J3FulfillmentErrorCodes.Configuration
        };

    private static string MapUserMessage(string reasonCode, string fallback) =>
        reasonCode switch
        {
            J3FulfillmentEligibilityCodes.FeatureDisabled =>
                "Integração J3 está desabilitada.",
            J3FulfillmentEligibilityCodes.MissingFiscalInvoice =>
                "NF-e autorizada necessária.",
            J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized =>
                "NF-e autorizada necessária.",
            J3FulfillmentEligibilityCodes.MissingNfeKey or J3FulfillmentEligibilityCodes.InvalidNfeKey =>
                "Chave da NF-e inválida.",
            J3FulfillmentEligibilityCodes.IncompleteShippingAddress =>
                "Endereço de entrega incompleto.",
            J3FulfillmentEligibilityCodes.MissingResidentialFlag =>
                "Informe se o endereço é residencial ou comercial.",
            J3FulfillmentEligibilityCodes.WrongShippingMethod =>
                "Pedido não utiliza frete J3.",
            J3FulfillmentEligibilityCodes.PaymentNotApproved =>
                "Pagamento ainda não aprovado.",
            J3FulfillmentEligibilityCodes.FulfillmentAlreadyCreated =>
                "Pedido já enviado para a J3.",
            J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists =>
                "Fulfillment J3 já está em processamento.",
            J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview =>
                "Resultado incerto. Não reenviar automaticamente.",
            J3FulfillmentEligibilityCodes.RetryableFailureNotAutoRetried =>
                "Falha anterior exige revisão; retry automático não disponível nesta fase.",
            _ => fallback
        };
}
