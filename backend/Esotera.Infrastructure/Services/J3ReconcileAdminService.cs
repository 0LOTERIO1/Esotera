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
/// Reconciliação admin: searchOrderByCode (schema real) + update do J3Fulfillment existente.
/// Zero createTmsOrders / importOrderByAccessKey.
/// </summary>
public sealed class J3ReconcileAdminService : IJ3ReconcileAdminService
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3OrderLookupClient _lookup;
    private readonly ILogger<J3ReconcileAdminService> _logger;

    public J3ReconcileAdminService(
        EsoteraDbContext context,
        IJ3OrderLookupClient lookup,
        ILogger<J3ReconcileAdminService> logger)
    {
        _context = context;
        _lookup = lookup;
        _logger = logger;
    }

    public async Task<J3ReconcileAdminOutcome> ReconcileAsync(
        Guid orderId,
        J3ReconcileConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return J3ReconcileAdminOutcome.NotFound();

        var confirmOrder = request.ConfirmOrderNumber?.Trim() ?? string.Empty;
        var confirmCode = request.ConfirmJ3OrderCode?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(confirmOrder)
            || !string.Equals(confirmOrder, order.OrderNumber, StringComparison.Ordinal))
        {
            return J3ReconcileAdminOutcome.BadRequest(
                J3ReconcileErrorCodes.ConfirmMismatch,
                "Confirmação do número do pedido inválida.");
        }

        if (string.IsNullOrWhiteSpace(confirmCode))
        {
            return J3ReconcileAdminOutcome.BadRequest(
                J3ReconcileErrorCodes.ConfirmMismatch,
                "Confirmação do código J3 obrigatória.");
        }

        var fulfillment = await _context.J3Fulfillments
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
        if (fulfillment is null)
        {
            return J3ReconcileAdminOutcome.Conflict(
                J3ReconcileErrorCodes.NotEligible,
                "J3Fulfillment não encontrado para o pedido.");
        }

        // Idempotência: Created com mesmo J3OrderId + J3OrderCode.
        // Sem lookup se já temos identidade completa; se só temos code, ainda exige id.
        if (string.Equals(fulfillment.Status, J3FulfillmentStatus.Created, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(fulfillment.J3OrderId)
                && J3ReconcileMatcher.CodesEqual(fulfillment.J3OrderCode, confirmCode))
            {
                // Id já conhecido — idempotente sem rewrite (mesmo code + id presente).
                // Caller tipicamente reenvia o mesmo confirm; se id diverge de um segundo reconcile
                // com outro código já tratado abaixo.
                _logger.LogInformation(
                    "J3 reconcile already done order {OrderId} fulfillment {FulfillmentId} j3Code {J3OrderCode} j3OrderId {J3OrderId} store {StoreName}",
                    order.Id,
                    fulfillment.Id,
                    fulfillment.J3OrderCode,
                    fulfillment.J3OrderId,
                    "(local)");
                return J3ReconcileAdminOutcome.Ok(BuildBody(order, fulfillment, already: true, lookupSent: false));
            }

            return ConflictSnap(
                order,
                fulfillment,
                J3ReconcileErrorCodes.CodeMismatch,
                "Fulfillment já Created com identidade J3 diferente.",
                lookupSent: false);
        }

        if (!string.Equals(fulfillment.Status, J3FulfillmentStatus.UnknownOutcome, StringComparison.Ordinal)
            || !string.Equals(
                fulfillment.LastErrorCode,
                J3FulfillmentErrorCodes.GraphqlAmbiguous,
                StringComparison.Ordinal))
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3ReconcileErrorCodes.NotEligible,
                "Reconciliação exige unknown_outcome com LastErrorCode GRAPHQL_AMBIGUOUS.",
                lookupSent: false);
        }

        var lookup = await _lookup.SearchByCodeAsync(confirmCode, cancellationToken);
        if (lookup.Outcome == J3OrderLookupOutcome.NotFound || lookup.Response is null)
        {
            return ConflictSnap(
                order,
                fulfillment,
                J3ReconcileErrorCodes.NotFound,
                "Pedido J3 não encontrado pelo código informado.",
                lookupSent: true);
        }

        if (lookup.Outcome == J3OrderLookupOutcome.Failed)
        {
            return ConflictSnap(
                order,
                fulfillment,
                lookup.ErrorCode ?? J3ReconcileErrorCodes.LookupFailed,
                "Falha no lookup read-only J3.",
                lookupSent: true);
        }

        var (snapshot, matchError) = J3ReconcileMatcher.TryBuildSnapshot(
            order,
            lookup.Response,
            confirmCode);
        if (matchError is not null || snapshot is null)
        {
            _logger.LogWarning(
                "J3 reconcile mismatch order {OrderId} fulfillment {FulfillmentId} j3Code {J3OrderCode} error {ErrorCode} store {StoreName}",
                order.Id,
                fulfillment.Id,
                confirmCode,
                matchError,
                lookup.Response.StoreName);
            return ConflictSnap(
                order,
                fulfillment,
                matchError ?? J3ReconcileErrorCodes.LookupFailed,
                "Dados do pedido J3 divergem do pedido Esotera.",
                lookupSent: true);
        }

        var now = DateTime.UtcNow;
        fulfillment.Status = J3FulfillmentStatus.Created;
        fulfillment.J3OrderId = snapshot.OrderId;
        fulfillment.J3OrderCode = snapshot.OrderCode;
        fulfillment.J3TrackingNumber = snapshot.TrackingNumber;
        // DeliveryPointId / StampUrl: schema não fornece — não inventar / não alterar.
        fulfillment.CompletedAtUtc = now;
        fulfillment.LastErrorCode = null;
        fulfillment.LastErrorAtUtc = null;
        fulfillment.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "J3 reconcile succeeded order {OrderId} fulfillment {FulfillmentId} j3Code {J3OrderCode} j3OrderId {J3OrderId} store {StoreName} ecommerce {Ecommerce} remoteStatus {RemoteStatus}",
            order.Id,
            fulfillment.Id,
            snapshot.OrderCode,
            snapshot.OrderId,
            snapshot.StoreName,
            snapshot.Ecommerce,
            snapshot.Status);

        return J3ReconcileAdminOutcome.Ok(BuildBody(order, fulfillment, already: false, lookupSent: true));
    }

    private static J3ReconcileAdminOutcome ConflictSnap(
        Order order,
        J3Fulfillment fulfillment,
        string reasonCode,
        string message,
        bool lookupSent)
    {
        return J3ReconcileAdminOutcome.Conflict(
            reasonCode,
            message,
            BuildBody(order, fulfillment, already: false, lookupSent: lookupSent, outcome: "Conflict", error: reasonCode));
    }

    private static J3ReconcileAdminResultDto BuildBody(
        Order order,
        J3Fulfillment fulfillment,
        bool already,
        bool lookupSent,
        string outcome = "Success",
        string? error = null) =>
        new(
            order.Id,
            order.OrderNumber,
            fulfillment.Id,
            fulfillment.Status,
            fulfillment.LastErrorCode,
            fulfillment.J3OrderId,
            fulfillment.J3OrderCode,
            fulfillment.J3TrackingNumber,
            already,
            outcome,
            error,
            lookupSent,
            J3SearchOrderByCodeQuery.OperationName);
}
