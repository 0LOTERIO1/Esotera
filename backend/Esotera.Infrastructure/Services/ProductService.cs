using System.Text.Json;
using System.Text.RegularExpressions;
using Esotera.Application.DTOs.Products;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Esotera.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly EsoteraDbContext _context;
    private readonly IProductImageStorage _imageStorage;
    private readonly ILogger<ProductService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public ProductService(
        EsoteraDbContext context,
        IProductImageStorage imageStorage,
        ILogger<ProductService> logger)
    {
        _context = context;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductListDto>> ListAsync(bool availableOnly = true, bool includeArchived = false)
    {
        var query = BaseProductQuery();

        if (!includeArchived)
            query = query.Where(p => !p.IsArchived);

        if (availableOnly)
            query = query.Where(p => p.IsAvailable);

        return await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(ToListDtoExpression())
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductListDto>> AdminListAsync(AdminProductFilterRequest filter)
    {
        var query = BaseProductQuery();

        if (!filter.IncludeAllArchiveStates)
        {
            if (filter.IsArchived == true)
                query = query.Where(p => p.IsArchived);
            else
                query = query.Where(p => !p.IsArchived);
        }

        if (filter.IsAvailable.HasValue)
            query = query.Where(p => p.IsAvailable == filter.IsAvailable.Value);

        if (filter.IsFeatured.HasValue)
            query = query.Where(p => p.IsFeatured == filter.IsFeatured.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                p.Slug.ToLower().Contains(search));
        }

        return await query
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Select(ToListDtoExpression())
            .ToListAsync();
    }

    public async Task<ProductDto?> GetBySlugAsync(string slug)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsArchived);

        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, bool includeArchived = false)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(p => !p.IsArchived);

        var product = await query.FirstOrDefaultAsync(p => p.Id == id);
        return product == null ? null : MapToDto(product);
    }

    public async Task<ProductDto> AdminCreateAsync(CreateProductRequest request)
    {
        var slug = NormalizeSlug(request.Slug);
        EnsureValidSlug(slug);

        var slugExists = await _context.Products.AnyAsync(p => p.Slug == slug);
        if (slugExists)
            throw new ConflictException($"Produto com slug '{slug}' já existe.");

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
            throw new NotFoundException("Categoria", request.CategoryId);

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name.Trim(),
            ShortDescription = request.ShortDescription,
            Description = request.Description,
            Price = request.Price,
            CategoryId = request.CategoryId,
            FeaturesJson = SerializeArray(request.Features),
            PackageContentsJson = SerializeArray(request.PackageContents),
            VariationsJson = request.Variations != null
                ? JsonSerializer.Serialize(request.Variations, JsonOptions)
                : null,
            IsFeatured = request.IsFeatured,
            IsAvailable = request.IsAvailable,
            IsDemo = request.IsDemo,
            IsArchived = false,
            RowVersion = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return (await GetByIdAsync(product.Id, includeArchived: true))!;
    }

    public async Task<ProductDto> AdminUpdateAsync(Guid id, UpdateProductRequest request)
    {
        var product = await LoadProductForAdminAsync(id);

        if (request.ExpectedVersion.HasValue && product.RowVersion != request.ExpectedVersion.Value)
        {
            throw new ConflictException(
                "O produto foi alterado por outra operação. Atualize os dados e tente novamente.");
        }

        if (request.Slug != null)
        {
            var slug = NormalizeSlug(request.Slug);
            EnsureValidSlug(slug);
            if (slug != product.Slug)
            {
                var slugExists = await _context.Products.AnyAsync(p => p.Slug == slug && p.Id != id);
                if (slugExists)
                    throw new ConflictException($"Produto com slug '{slug}' já existe.");
                product.Slug = slug;
            }
        }

        if (request.CategoryId.HasValue && request.CategoryId != product.CategoryId)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
            if (!categoryExists)
                throw new NotFoundException("Categoria", request.CategoryId);
            product.CategoryId = request.CategoryId.Value;
        }

        if (request.Name != null) product.Name = request.Name.Trim();
        if (request.ShortDescription != null) product.ShortDescription = request.ShortDescription;
        if (request.Description != null) product.Description = request.Description;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.Features != null) product.FeaturesJson = SerializeArray(request.Features);
        if (request.PackageContents != null) product.PackageContentsJson = SerializeArray(request.PackageContents);
        if (request.Variations != null)
            product.VariationsJson = JsonSerializer.Serialize(request.Variations, JsonOptions);
        if (request.IsFeatured.HasValue) product.IsFeatured = request.IsFeatured.Value;
        if (request.IsAvailable.HasValue) product.IsAvailable = request.IsAvailable.Value;
        if (request.IsDemo.HasValue) product.IsDemo = request.IsDemo.Value;

        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "O produto foi alterado por outra operação. Atualize os dados e tente novamente.");
        }

        await _context.Entry(product).Reference(p => p.Category).LoadAsync();
        return MapToDto(product);
    }

    public async Task SetAvailabilityAsync(Guid id, bool isAvailable)
    {
        var product = await _context.Products.FindAsync(id)
            ?? throw new NotFoundException("Produto", id);

        if (product.IsArchived && isAvailable)
            throw new ValidationException("isAvailable", "Não é possível disponibilizar um produto arquivado. Restaure-o antes.");

        product.IsAvailable = isAvailable;
        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task SetFeaturedAsync(Guid id, bool isFeatured)
    {
        var product = await _context.Products.FindAsync(id)
            ?? throw new NotFoundException("Produto", id);

        product.IsFeatured = isFeatured;
        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<ProductDto> ArchiveAsync(Guid id)
    {
        var product = await LoadProductForAdminAsync(id);
        if (product.IsArchived)
            return MapToDto(product);

        product.IsArchived = true;
        product.ArchivedAtUtc = DateTime.UtcNow;
        product.IsAvailable = false;
        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToDto(product);
    }

    public async Task<ProductDto> RestoreAsync(Guid id)
    {
        var product = await LoadProductForAdminAsync(id);
        if (!product.IsArchived)
            return MapToDto(product);

        product.IsArchived = false;
        product.ArchivedAtUtc = null;
        // Não restaura IsAvailable automaticamente — permanece false até decisão explícita
        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapToDto(product);
    }

    public async Task<ProductImageDto> AddImageAsync(
        Guid productId,
        Stream imageStream,
        string contentType,
        string fileName,
        bool isPrimary = false,
        string? altText = null,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadProductForAdminAsync(productId);

        if (product.Images.Count >= ProductImageLimits.MaxImagesPerProduct)
        {
            throw new ValidationException(
                "file",
                $"Limite de {ProductImageLimits.MaxImagesPerProduct} imagens por produto atingido.");
        }

        ProductImageUploadResult upload;
        try
        {
            upload = await _imageStorage.UploadAsync(imageStream, contentType, fileName, cancellationToken);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no upload de imagem do produto {ProductId}", productId);
            throw new ValidationException("file", "Não foi possível enviar a imagem. Tente novamente.");
        }

        try
        {
            if (isPrimary || !product.Images.Any())
            {
                foreach (var img in product.Images)
                    img.IsPrimary = false;
            }

            var maxSortOrder = product.Images.Any() ? product.Images.Max(i => i.SortOrder) : 0;
            var image = new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SecureUrl = upload.SecureUrl,
                PublicId = upload.PublicId,
                AltText = altText,
                SortOrder = maxSortOrder + 1,
                IsPrimary = isPrimary || !product.Images.Any(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.ProductImages.Add(image);
            product.RowVersion += 1;
            product.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return MapImageDto(image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao persistir imagem; iniciando compensação Cloudinary");
            try
            {
                await _imageStorage.DeleteAsync(upload.PublicId, cancellationToken);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Compensação de upload falhou; limpeza pendente");
            }

            if (ex is ValidationException or ConflictException or NotFoundException)
                throw;

            throw new ValidationException("file", "Não foi possível salvar a imagem. Tente novamente.");
        }
    }

    public async Task<ProductImageDto> UpdateImageAsync(
        Guid productId,
        Guid imageId,
        UpdateProductImageRequest request)
    {
        var product = await LoadProductForAdminAsync(productId);
        var image = product.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException("Imagem", imageId);

        if (request.AltText != null)
            image.AltText = request.AltText;

        if (request.IsPrimary == true)
        {
            foreach (var img in product.Images)
                img.IsPrimary = img.Id == imageId;
        }

        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return MapImageDto(image);
    }

    public async Task DeleteImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var product = await LoadProductForAdminAsync(productId);
        var image = product.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException("Imagem", imageId);

        var publicId = image.PublicId;
        var wasPrimary = image.IsPrimary;

        _context.ProductImages.Remove(image);
        product.Images.Remove(image);

        if (wasPrimary && product.Images.Any())
        {
            var next = product.Images.OrderBy(i => i.SortOrder).First();
            next.IsPrimary = true;
        }

        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Remoção no storage depois da persistência (não desfaz se falhar)
        try
        {
            await _imageStorage.DeleteAsync(publicId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Imagem removida do banco, mas limpeza no storage falhou");
        }
    }

    public async Task<IReadOnlyList<ProductImageDto>> ReorderImagesAsync(
        Guid productId,
        ReorderProductImagesRequest request)
    {
        var product = await LoadProductForAdminAsync(productId);
        var ids = request.ImageIds;

        if (ids.Length != product.Images.Count ||
            ids.Any(id => product.Images.All(i => i.Id != id)))
        {
            throw new ValidationException("imageIds", "A lista deve conter exatamente todas as imagens do produto.");
        }

        for (var i = 0; i < ids.Length; i++)
        {
            var image = product.Images.First(img => img.Id == ids[i]);
            image.SortOrder = i + 1;
            image.IsPrimary = i == 0;
        }

        product.RowVersion += 1;
        product.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return product.Images
            .OrderBy(i => i.SortOrder)
            .Select(MapImageDto)
            .ToList();
    }

    private IQueryable<Product> BaseProductQuery() =>
        _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .AsQueryable();

    private static System.Linq.Expressions.Expression<Func<Product, ProductListDto>> ToListDtoExpression() =>
        p => new ProductListDto(
            p.Id,
            p.Slug,
            p.Name,
            p.ShortDescription,
            p.Price,
            p.Category.Name,
            p.CategoryId,
            p.Images.Where(i => i.IsPrimary).Select(i => i.SecureUrl).FirstOrDefault()
                ?? p.Images.OrderBy(i => i.SortOrder).Select(i => i.SecureUrl).FirstOrDefault(),
            p.IsFeatured,
            p.IsAvailable,
            p.IsArchived,
            p.UpdatedAtUtc
        );

    private async Task<Product> LoadProductForAdminAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NotFoundException("Produto", id);
    }

    private static string NormalizeSlug(string slug) =>
        slug.Trim().ToLowerInvariant();

    private static void EnsureValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || !SlugRegex.IsMatch(slug))
            throw new ValidationException("slug", "Slug deve conter apenas letras minúsculas, números e hífens.");
    }

    private static string? SerializeArray(string[]? values) =>
        values != null ? JsonSerializer.Serialize(values, JsonOptions) : null;

    private static ProductImageDto MapImageDto(ProductImage image) =>
        new(
            image.Id,
            image.SecureUrl,
            image.PublicId,
            image.AltText,
            image.SortOrder,
            image.IsPrimary,
            image.CreatedAtUtc
        );

    private static ProductDto MapToDto(Product product)
    {
        string[]? features = null;
        string[]? packageContents = null;
        ProductVariationDto[]? variations = null;

        if (!string.IsNullOrEmpty(product.FeaturesJson))
            features = JsonSerializer.Deserialize<string[]>(product.FeaturesJson, JsonOptions);

        if (!string.IsNullOrEmpty(product.PackageContentsJson))
            packageContents = JsonSerializer.Deserialize<string[]>(product.PackageContentsJson, JsonOptions);

        if (!string.IsNullOrEmpty(product.VariationsJson))
            variations = JsonSerializer.Deserialize<ProductVariationDto[]>(product.VariationsJson, JsonOptions);

        var images = product.Images
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.SortOrder)
            .Select(MapImageDto)
            .ToArray();

        return new ProductDto(
            product.Id,
            product.Slug,
            product.Name,
            product.ShortDescription,
            product.Description,
            product.Price,
            product.Category.Name,
            product.CategoryId,
            images,
            features,
            packageContents,
            variations,
            product.IsFeatured,
            product.IsAvailable,
            product.IsArchived,
            product.ArchivedAtUtc,
            product.IsDemo,
            product.RowVersion,
            product.CreatedAtUtc,
            product.UpdatedAtUtc
        );
    }
}
