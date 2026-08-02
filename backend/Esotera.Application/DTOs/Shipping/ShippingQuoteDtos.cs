namespace Esotera.Application.DTOs.Shipping;

/// <summary>
/// Request público de cotação. Frontend NÃO envia preço de frete.
/// </summary>
public record ShippingQuoteRequest(
    string DestinationCep,
    string State,
    decimal ProductsSubtotal
);

public record ShippingQuoteResponse(
    bool Ok,
    ShippingQuoteOptionDto[] Options,
    string? ErrorCode,
    string? Message
);

public record ShippingQuoteOptionDto(
    string Id,
    string Provider,
    string Name,
    decimal Price,
    decimal OriginalPrice,
    string EstimatedDays,
    int EstimatedDaysMin,
    int EstimatedDaysMax,
    string Description,
    bool FreeShippingApplied,
    bool SubsidyApplied
);
