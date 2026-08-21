namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Comando local: um pedido Esotera → um CreateTmsOrderInput (array GraphQL de length 1).
/// </summary>
public sealed class J3CreateTmsOrderCommand
{
    public required Guid LocalOrderId { get; init; }
    public required J3CreateTmsOrderInputDto Input { get; init; }
}

/// <summary>
/// CreateTmsOrderInput alinhado ao schema J3 (introspecção):
/// sellerId, orderPickupType, quantity, sellerInformationId, totalPackageValueInCents, deliveryPoint,
/// e opcionais fiscais nf / nfKey / nfSeries (metadata FiscalInvoice — sem XML/danfe).
/// Nulos omitidos (WhenWritingNull): danfe/ecommerce/packages/nro/tracking/shipment/etc.
/// </summary>
public sealed class J3CreateTmsOrderInputDto
{
    public required string SellerId { get; init; }
    public required string SellerInformationId { get; init; }
    public required string OrderPickupType { get; init; }
    public required int Quantity { get; init; }
    public required int TotalPackageValueInCents { get; init; }
    public required J3DeliveryPointInputDto DeliveryPoint { get; init; }

    /// <summary>Número da NF-e (<see cref="Domain.Entities.FiscalInvoice.Number"/>). Schema: String opcional.</summary>
    public string? Nf { get; init; }

    /// <summary>ChNFe 44 dígitos. Schema: nfKey String opcional.</summary>
    public string? NfKey { get; init; }

    /// <summary>Série da NF-e. Schema: nfSeries String opcional.</summary>
    public string? NfSeries { get; init; }
}

/// <summary>
/// DeliveryPointInput. Schema J3: <c>addressDistric</c> (grafia da API).
/// </summary>
public sealed class J3DeliveryPointInputDto
{
    public required string AddressStreet { get; init; }
    public required string AddressNumber { get; init; }
    public string? AddressComplement { get; init; }
    public required string AddressDistric { get; init; }
    public required string AddressCity { get; init; }
    public required string AddressState { get; init; }
    public required string AddressZipCode { get; init; }
    public required string ContactName { get; init; }
    public string? ContactPhoneNumber { get; init; }
    public required bool IsResidentialAddress { get; init; }
}

/// <summary>
/// Passivo: CreatePackagesTmsOrderInput. Portal Avulso Standalone não envia packages (Passo 4.2B).
/// Não usado no payload atual.
/// </summary>
public sealed class J3PackageInputDto
{
    public required int HeightInCentimeters { get; init; }
    public required bool IsFragile { get; init; }
    public required bool IsValuable { get; init; }
    public required int ItemValueInCents { get; init; }
    public required int LengthInCentimeters { get; init; }
    public required int Quantity { get; init; }
    public required int TotalPackageEstimatedValueInCents { get; init; }
    public required string Type { get; init; }
    public required int WeightInGrams { get; init; }
    public required int WidthInCentimeters { get; init; }
}

/// <summary>
/// Recorte sanitizado de ApiError. Sem description (pode conter dados sensíveis).
/// </summary>
public sealed class J3CreateTmsOrdersApiError
{
    public string? Layer { get; init; }
    public string? ClientId { get; init; }
    public string? ErrorCode { get; init; }
}
