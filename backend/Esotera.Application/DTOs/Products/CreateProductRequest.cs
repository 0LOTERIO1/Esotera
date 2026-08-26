namespace Esotera.Application.DTOs.Products;

public record CreateProductRequest(
    string Name,
    string Slug,
    string? ShortDescription,
    string? Description,
    decimal Price,
    Guid CategoryId,
    string[]? Features,
    string[]? PackageContents,
    ProductVariationDto[]? Variations,
    bool IsFeatured = false,
    bool IsAvailable = true,
    bool IsDemo = false,
    string? Sku = null
);

public record UpdateProductRequest(
    string? Name,
    string? Slug,
    string? ShortDescription,
    string? Description,
    decimal? Price,
    Guid? CategoryId,
    string[]? Features,
    string[]? PackageContents,
    ProductVariationDto[]? Variations,
    bool? IsFeatured,
    bool? IsAvailable,
    bool? IsDemo,
    long? ExpectedVersion,
    string? Sku = null
);

public record UpdateProductImageRequest(
    string? AltText,
    bool? IsPrimary
);

public record ReorderProductImagesRequest(
    Guid[] ImageIds
);

public record AdminProductFilterRequest(
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsAvailable = null,
    /// <summary>
    /// null = não arquivados (padrão); true = só arquivados; false = só não arquivados.
    /// Use IncludeAllArchiveStates=true para listar ambos.
    /// </summary>
    bool? IsArchived = null,
    bool? IsFeatured = null,
    bool IncludeAllArchiveStates = false
);
