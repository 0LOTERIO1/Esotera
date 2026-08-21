using Esotera.Application.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Options;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Mapper puro Order + options (+ snapshot fiscal) → CreateTmsOrderInput (Pedido Avulso).
/// Sem HTTP. Sem XmlCipher/decriptação. Sem danfe/ecommerce/packages/nro/tracking/shipment.
/// StoreSettings permanece na assinatura (compat) mas não entra no payload Avulso.
/// </summary>
public static class J3CreateTmsOrderMapper
{
    public static J3CreateTmsOrderBuildResult TryBuild(
        Order order,
        StoreSettings settings,
        J3ShippingOptions options,
        J3FiscalEligibilitySnapshot? fiscal = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);

        var sellerId = options.SellerId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sellerId))
            return J3CreateTmsOrderBuildResult.Fail(J3FulfillmentErrorCodes.MissingSellerId);

        var sellerInformationId = options.SellerInformationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sellerInformationId))
            return J3CreateTmsOrderBuildResult.Fail(J3FulfillmentErrorCodes.MissingSellerInformationId);

        if (order.ShippingIsResidentialAddress is null)
            return J3CreateTmsOrderBuildResult.Fail(J3FulfillmentErrorCodes.ResidentialRequired);

        var cepDigits = BrazilianCep.TryNormalize(order.ShipCep);
        if (cepDigits is null)
            return J3CreateTmsOrderBuildResult.Fail(J3FulfillmentErrorCodes.InvalidCep);

        if (string.IsNullOrWhiteSpace(order.ShipStreet)
            || string.IsNullOrWhiteSpace(order.ShipNumber)
            || string.IsNullOrWhiteSpace(order.ShipNeighborhood)
            || string.IsNullOrWhiteSpace(order.ShipCity)
            || string.IsNullOrWhiteSpace(order.ShipState)
            || string.IsNullOrWhiteSpace(order.CustomerName))
        {
            return J3CreateTmsOrderBuildResult.Fail(J3FulfillmentErrorCodes.MissingAddress);
        }

        var merchandiseCents = J3MerchandiseValue.ToCents(order.Subtotal, order.Discount);
        var pickup = string.IsNullOrWhiteSpace(options.OrderPickupType)
            ? "Standard"
            : options.OrderPickupType.Trim();

        var phone = string.IsNullOrWhiteSpace(order.CustomerPhone) ? null : order.CustomerPhone.Trim();
        var complement = string.IsNullOrWhiteSpace(order.ShipComplement) ? null : order.ShipComplement.Trim();

        string? nf = null;
        string? nfKey = null;
        string? nfSeries = null;
        if (fiscal is not null)
        {
            var ch = fiscal.ChNFe?.Trim();
            if (!string.IsNullOrEmpty(ch) && J3FulfillmentEligibility.IsValidChNFe(ch))
                nfKey = ch;

            if (!string.IsNullOrWhiteSpace(fiscal.Number))
                nf = fiscal.Number.Trim();

            if (!string.IsNullOrWhiteSpace(fiscal.Series))
                nfSeries = fiscal.Series.Trim();
        }

        var input = new J3CreateTmsOrderInputDto
        {
            SellerId = sellerId,
            SellerInformationId = sellerInformationId,
            OrderPickupType = pickup,
            Quantity = 1,
            TotalPackageValueInCents = merchandiseCents,
            DeliveryPoint = new J3DeliveryPointInputDto
            {
                AddressStreet = order.ShipStreet.Trim(),
                AddressNumber = order.ShipNumber.Trim(),
                AddressComplement = complement,
                AddressDistric = order.ShipNeighborhood.Trim(),
                AddressCity = order.ShipCity.Trim(),
                AddressState = order.ShipState.Trim(),
                AddressZipCode = BrazilianCep.FormatMasked(cepDigits),
                ContactName = order.CustomerName.Trim(),
                ContactPhoneNumber = phone,
                IsResidentialAddress = order.ShippingIsResidentialAddress.Value
            },
            Nf = nf,
            NfKey = nfKey,
            NfSeries = nfSeries
        };

        return J3CreateTmsOrderBuildResult.Ok(new J3CreateTmsOrderCommand
        {
            LocalOrderId = order.Id,
            Input = input
        });
    }
}

public sealed class J3CreateTmsOrderBuildResult
{
    public bool IsValid { get; private init; }
    public J3CreateTmsOrderCommand? Command { get; private init; }
    public string? ErrorCode { get; private init; }

    public static J3CreateTmsOrderBuildResult Ok(J3CreateTmsOrderCommand command) =>
        new() { IsValid = true, Command = command };

    public static J3CreateTmsOrderBuildResult Fail(string errorCode) =>
        new()
        {
            IsValid = false,
            ErrorCode = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Configuration
        };
}
