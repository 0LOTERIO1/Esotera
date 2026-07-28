using Esotera.Application.DTOs.Products;

namespace Esotera.Application.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductListDto>> ListAsync(bool availableOnly = true, bool includeArchived = false);
    Task<IReadOnlyList<ProductListDto>> AdminListAsync(AdminProductFilterRequest filter);
    Task<ProductDto?> GetBySlugAsync(string slug);
    Task<ProductDto?> GetByIdAsync(Guid id, bool includeArchived = false);
    Task<ProductDto> AdminCreateAsync(CreateProductRequest request);
    Task<ProductDto> AdminUpdateAsync(Guid id, UpdateProductRequest request);
    Task SetAvailabilityAsync(Guid id, bool isAvailable);
    Task SetFeaturedAsync(Guid id, bool isFeatured);
    Task<ProductDto> ArchiveAsync(Guid id);
    Task<ProductDto> RestoreAsync(Guid id);
    Task<ProductImageDto> AddImageAsync(
        Guid productId,
        Stream imageStream,
        string contentType,
        string fileName,
        bool isPrimary = false,
        string? altText = null,
        CancellationToken cancellationToken = default);
    Task<ProductImageDto> UpdateImageAsync(Guid productId, Guid imageId, UpdateProductImageRequest request);
    Task DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductImageDto>> ReorderImagesAsync(Guid productId, ReorderProductImagesRequest request);
}
