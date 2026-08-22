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
/// Sync manual de status logístico J3 via searchOrderByCode.
/// Não altera J3Fulfillment.Status (integração). Zero mutations J3.
/// </summary>
public sealed class J3TrackingSyncService : IJ3TrackingSyncService
{
    private readonly EsoteraDbContext _context;
    private readonly IJ3OrderLookupClient _lookup;
    private readonly ILogger<J3TrackingSyncService> _logger;

    public J3TrackingSyncService(
        EsoteraDbContext context,
        IJ3OrderLookupClient lookup,
        ILogger<J3TrackingSyncService> logger)
    {
        _context = context;
        _lookup = lookup;
        _logger = logger;
    }

    public async Task<J3TrackingSyncOutcome> SyncAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return J3TrackingSyncOutcome.NotFound();

        var fulfillment = await _context.J3Fulfillments
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);
        if (fulfillment is null)
        {
            return J3TrackingSyncOutcome.Conflict(
                J3TrackingSyncErrorCodes.NotEligible,
                "J3Fulfillment não encontrado para o pedido.");
        }

        if (!string.Equals(fulfillment.Status, J3FulfillmentStatus.Created, StringComparison.Ordinal))
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3TrackingSyncErrorCodes.NotEligible,
                "Sincronização de tracking exige fulfillment created.");
        }

        if (string.IsNullOrWhiteSpace(fulfillment.J3OrderCode))
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3TrackingSyncErrorCodes.NotEligible,
                "J3OrderCode ausente — sync de tracking não elegível.");
        }

        // Identificadores locais contraditórios: fail closed antes de qualquer HTTP J3.
        if (!string.IsNullOrWhiteSpace(fulfillment.J3TrackingNumber)
            && !J3ReconcileMatcher.CodesEqual(fulfillment.J3OrderCode, fulfillment.J3TrackingNumber))
        {
            return ConflictNoLookup(
                order,
                fulfillment,
                J3TrackingSyncErrorCodes.LocalCodeMismatch,
                "J3OrderCode e J3TrackingNumber locais divergem.");
        }

        var code = fulfillment.J3OrderCode.Trim();
        var lookup = await _lookup.SearchByCodeAsync(code, cancellationToken);

        if (lookup.Outcome == J3OrderLookupOutcome.Failed)
        {
            // Lookup HTTP/GraphQL: mapear genérico RECONCILE_LOOKUP_FAILED → TRACKING_SYNC_LOOKUP_FAILED;
            // preservar HTTP_*/GRAPHQL_*/NETWORK_* sanitizados quando o client os devolver.
            var raw = lookup.ErrorCode ?? J3TrackingSyncErrorCodes.LookupFailed;
            var err = MapLookupFailureCode(raw);
            await PersistSyncErrorAsync(fulfillment, err, cancellationToken);
            return Unprocessable(
                order,
                fulfillment,
                err,
                "Falha no lookup read-only J3.",
                lookupSent: true);
        }

        if (lookup.Outcome == J3OrderLookupOutcome.NotFound || lookup.Response is null)
        {
            await PersistSyncErrorAsync(
                fulfillment,
                J3TrackingSyncErrorCodes.NotFound,
                cancellationToken);
            return Unprocessable(
                order,
                fulfillment,
                J3TrackingSyncErrorCodes.NotFound,
                "Pedido J3 não encontrado pelo código.",
                lookupSent: true);
        }

        var (remoteStatus, matchError) = J3TrackingSyncIdentity.TryValidate(
            order,
            fulfillment,
            lookup.Response);
        if (matchError is not null || remoteStatus is null)
        {
            var codeErr = J3FulfillmentErrorCodes.Sanitize(matchError)
                ?? J3TrackingSyncErrorCodes.LookupFailed;
            _logger.LogWarning(
                "J3 tracking sync identity fail order {OrderId} fulfillment {FulfillmentId} j3Code {J3OrderCode} error {ErrorCode}",
                order.Id,
                fulfillment.Id,
                code,
                codeErr);
            await PersistSyncErrorAsync(fulfillment, codeErr, cancellationToken);
            return Unprocessable(
                order,
                fulfillment,
                codeErr,
                "Identidade do pedido J3 divergente ou inválida.",
                lookupSent: true);
        }

        var now = DateTime.UtcNow;
        fulfillment.J3RemoteStatus = remoteStatus;
        fulfillment.J3LastStatusSyncAtUtc = now;
        fulfillment.J3LastStatusSyncErrorCode = null;
        fulfillment.J3LastStatusSyncErrorAtUtc = null;
        fulfillment.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "J3 tracking sync succeeded order {OrderId} fulfillment {FulfillmentId} j3Code {J3OrderCode} remoteStatus {RemoteStatus}",
            order.Id,
            fulfillment.Id,
            code,
            remoteStatus);

        return J3TrackingSyncOutcome.Ok(BuildBody(order, fulfillment, outcome: "Success", error: null, lookupSent: true));
    }

    private static string MapLookupFailureCode(string raw)
    {
        var sanitized = J3FulfillmentErrorCodes.Sanitize(raw)
            ?? J3TrackingSyncErrorCodes.LookupFailed;
        if (string.Equals(sanitized, J3ReconcileErrorCodes.LookupFailed, StringComparison.Ordinal))
        {
            return J3TrackingSyncErrorCodes.LookupFailed;
        }

        return sanitized;
    }

    private async Task PersistSyncErrorAsync(
        J3Fulfillment fulfillment,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // Preserva J3RemoteStatus e J3LastStatusSyncAtUtc.
        fulfillment.J3LastStatusSyncErrorCode = J3FulfillmentErrorCodes.Sanitize(errorCode)
            ?? J3TrackingSyncErrorCodes.LookupFailed;
        fulfillment.J3LastStatusSyncErrorAtUtc = now;
        fulfillment.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static J3TrackingSyncOutcome ConflictNoLookup(
        Order order,
        J3Fulfillment fulfillment,
        string reasonCode,
        string message) =>
        J3TrackingSyncOutcome.Conflict(
            reasonCode,
            message,
            BuildBody(order, fulfillment, outcome: "Conflict", error: reasonCode, lookupSent: false));

    private static J3TrackingSyncOutcome Unprocessable(
        Order order,
        J3Fulfillment fulfillment,
        string reasonCode,
        string message,
        bool lookupSent) =>
        J3TrackingSyncOutcome.Unprocessable(
            reasonCode,
            message,
            BuildBody(order, fulfillment, outcome: "Failed", error: reasonCode, lookupSent: lookupSent));

    private static J3TrackingSyncResultDto BuildBody(
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
            fulfillment.J3RemoteStatus,
            fulfillment.J3LastStatusSyncAtUtc,
            fulfillment.J3LastStatusSyncErrorCode,
            fulfillment.J3LastStatusSyncErrorAtUtc,
            outcome,
            error,
            lookupSent,
            J3SearchOrderByCodeQuery.OperationName);
}
