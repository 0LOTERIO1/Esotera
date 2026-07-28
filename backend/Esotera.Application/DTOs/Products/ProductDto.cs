namespace Esotera.Application.DTOs.Products;

public record ProductImageDto(
    Guid Id,
    string SecureUrl,
    string? PublicId,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTime CreatedAtUtc
);

public record ProductDto(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string Category,
    Guid CategoryId,
    ProductImageDto[] Images,
    string[]? Features,
    string[]? PackageContents,
    ProductVariationDto[]? Variations,
    bool IsFeatured,
    bool IsAvailable,
    bool IsArchived,
    DateTime? ArchivedAtUtc,
    bool IsDemo,
    long RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record ProductVariationDto(
    string Type,
    string[] Options
);

public record ProductListDto(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    decimal Price,
    string Category,
    Guid CategoryId,
    string? PrimaryImage,
    bool IsFeatured,
    bool IsAvailable,
    bool IsArchived,
    DateTime UpdatedAtUtc
);
