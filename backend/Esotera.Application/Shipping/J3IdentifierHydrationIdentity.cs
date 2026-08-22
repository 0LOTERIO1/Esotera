using Esotera.Application.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Validação fail-closed para hidratação via getOrderDetails.
/// Extrai tracking; não persiste status remoto.
/// </summary>
public static class J3IdentifierHydrationIdentity
{
    /// <summary>
    /// Null errorCode = OK; Tracking é o valor trim a gravar em code e trackingNumber.
    /// </summary>
    public static (string? Tracking, string? ErrorCode) TryValidate(
        Order order,
        J3Fulfillment fulfillment,
        J3OrderDetailsDto response)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
            return (null, J3IdentifierHydrationErrorCodes.IdMismatch);

        if (!J3ReconcileMatcher.CodesEqual(fulfillment.J3OrderId, response.Id))
            return (null, J3IdentifierHydrationErrorCodes.IdMismatch);

        if (response.DeliveryPoint is null)
            return (null, J3IdentifierHydrationErrorCodes.DeliveryPointMissing);

        if (string.IsNullOrWhiteSpace(response.DeliveryPoint.TrackingNumber))
            return (null, J3IdentifierHydrationErrorCodes.TrackingMissing);

        var localCep = BrazilianCep.TryNormalize(order.ShipCep);
        if (localCep is null)
            return (null, J3IdentifierHydrationErrorCodes.ZipMismatch);

        var remoteCep = BrazilianCep.TryNormalize(response.DeliveryPoint.AddressZipCode);
        if (remoteCep is null
            || !string.Equals(localCep, remoteCep, StringComparison.Ordinal))
        {
            return (null, J3IdentifierHydrationErrorCodes.ZipMismatch);
        }

        return (response.DeliveryPoint.TrackingNumber.Trim(), null);
    }
}
