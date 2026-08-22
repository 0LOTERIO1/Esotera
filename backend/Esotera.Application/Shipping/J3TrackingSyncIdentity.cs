using Esotera.Application.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Validação fail-closed de identidade para tracking sync.
/// Reutiliza normalização CEP/código da reconciliação; códigos de erro próprios.
/// </summary>
public static class J3TrackingSyncIdentity
{
    /// <summary>
    /// Null errorCode = identidade OK; RemoteStatus é o valor RAW (trim) a persistir.
    /// </summary>
    public static (string? RemoteStatus, string? ErrorCode) TryValidate(
        Order order,
        J3Fulfillment fulfillment,
        J3SearchOrderByCodeResponseDto response)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
            return (null, J3TrackingSyncErrorCodes.MissingRemoteId);

        if (string.IsNullOrWhiteSpace(response.Status))
            return (null, J3TrackingSyncErrorCodes.StatusMissing);

        if (!string.IsNullOrWhiteSpace(fulfillment.J3OrderId)
            && !J3ReconcileMatcher.CodesEqual(fulfillment.J3OrderId, response.Id))
        {
            return (null, J3TrackingSyncErrorCodes.IdMismatch);
        }

        var confirmCode = fulfillment.J3OrderCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(confirmCode))
            return (null, J3TrackingSyncErrorCodes.NotEligible);

        var points = response.DeliveryPoints ?? [];
        if (points.Count == 0)
            return (null, J3TrackingSyncErrorCodes.DeliveryPointMissing);

        var localCep = BrazilianCep.TryNormalize(order.ShipCep);
        if (localCep is null)
            return (null, J3TrackingSyncErrorCodes.ZipMismatch);

        // Após consistência local (service), o tracking esperado é o J3OrderCode.
        // J3TrackingNumber, se preenchido, já foi validado como igual ao code.
        var trackingMatches = points
            .Where(p => J3ReconcileMatcher.CodesEqual(p.TrackingNumber, confirmCode))
            .ToList();

        if (trackingMatches.Count == 0)
            return (null, J3TrackingSyncErrorCodes.TrackingMismatch);

        if (trackingMatches.Count > 1)
        {
            var ceps = trackingMatches
                .Select(p => BrazilianCep.TryNormalize(p.AddressZipCode))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (ceps.Count > 1)
                return (null, J3TrackingSyncErrorCodes.Ambiguous);
        }

        var compatible = trackingMatches
            .Where(p => string.Equals(
                BrazilianCep.TryNormalize(p.AddressZipCode),
                localCep,
                StringComparison.Ordinal))
            .ToList();

        if (compatible.Count == 0)
            return (null, J3TrackingSyncErrorCodes.ZipMismatch);

        if (compatible.Count > 1)
            return (null, J3TrackingSyncErrorCodes.Ambiguous);

        // Status RAW — sem lowercasing / translation.
        return (response.Status.Trim(), null);
    }
}
