using Esotera.Application.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Match fail-closed contra schema real searchOrderByCode.
/// NF null permitido. Sem validação de valores/recipient/seller/stamp.
/// </summary>
public static class J3ReconcileMatcher
{
    /// <summary>
    /// Seleciona exatamente um deliveryPoint compatível e monta snapshot canônico.
    /// Null errorCode = sucesso.
    /// </summary>
    public static (J3RemoteOrderSnapshot? Snapshot, string? ErrorCode) TryBuildSnapshot(
        Order order,
        J3SearchOrderByCodeResponseDto response,
        string confirmJ3OrderCode)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
            return (null, J3ReconcileErrorCodes.MissingOrderId);

        if (string.IsNullOrWhiteSpace(confirmJ3OrderCode))
            return (null, J3ReconcileErrorCodes.ConfirmMismatch);

        var points = response.DeliveryPoints ?? [];
        if (points.Count == 0)
            return (null, J3ReconcileErrorCodes.DeliveryPointMissing);

        var localCep = BrazilianCep.TryNormalize(order.ShipCep);
        if (localCep is null)
            return (null, J3ReconcileErrorCodes.CepMismatch);

        var trackingMatches = points
            .Where(p => CodesEqual(p.TrackingNumber, confirmJ3OrderCode))
            .ToList();

        if (trackingMatches.Count == 0)
        {
            if (points.Any(p => string.IsNullOrWhiteSpace(p.TrackingNumber)))
                return (null, J3ReconcileErrorCodes.TrackingMismatch);
            return (null, J3ReconcileErrorCodes.TrackingMismatch);
        }

        if (trackingMatches.Count > 1)
        {
            var ceps = trackingMatches
                .Select(p => BrazilianCep.TryNormalize(p.AddressZipCode))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (ceps.Count > 1)
                return (null, J3ReconcileErrorCodes.Multiple);
        }

        var compatible = trackingMatches
            .Where(p => string.Equals(
                BrazilianCep.TryNormalize(p.AddressZipCode),
                localCep,
                StringComparison.Ordinal))
            .ToList();

        if (compatible.Count == 0)
            return (null, J3ReconcileErrorCodes.CepMismatch);

        if (compatible.Count > 1)
            return (null, J3ReconcileErrorCodes.Multiple);

        var chosen = compatible[0];
        var tracking = chosen.TrackingNumber!.Trim();
        var cepDigits = BrazilianCep.TryNormalize(chosen.AddressZipCode)!;

        return (
            new J3RemoteOrderSnapshot(
                OrderId: response.Id.Trim(),
                OrderCode: confirmJ3OrderCode.Trim(),
                TrackingNumber: tracking,
                Status: response.Status,
                StoreName: response.StoreName,
                Ecommerce: response.Ecommerce,
                Date: response.Date,
                Nf: response.Nf,
                DeliveryCepDigits: cepDigits),
            null);
    }

    public static bool CodesEqual(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool SameReconciledIdentity(
        string? localJ3OrderId,
        string? localJ3OrderCode,
        string expectedOrderId,
        string expectedOrderCode) =>
        CodesEqual(localJ3OrderId, expectedOrderId)
        && CodesEqual(localJ3OrderCode, expectedOrderCode);
}
