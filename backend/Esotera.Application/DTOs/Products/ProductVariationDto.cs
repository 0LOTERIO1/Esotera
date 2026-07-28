namespace Esotera.Application.DTOs.Products;

public record ProductVariationDto(
    string Id,
    string Name,
    decimal Price,
    bool IsAvailable = true,
    string? Sku = null,
    string? ImageUrl = null
);
