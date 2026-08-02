namespace Esotera.Application.Shipping;

public sealed record ShippingQuoteQuery(
    string DestinationCepDigits,
    string State,
    decimal ProductsTotalAfterDiscount);
