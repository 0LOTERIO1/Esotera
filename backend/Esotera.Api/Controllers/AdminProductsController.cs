using Esotera.Application.DTOs.Products;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;
    private readonly IValidator<ReorderProductImagesRequest> _reorderValidator;

    public AdminProductsController(
        IProductService productService,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator,
        IValidator<ReorderProductImagesRequest> reorderValidator)
    {
        _productService = productService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _reorderValidator = reorderValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductListDto>>> List(
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool? isAvailable = null,
        [FromQuery] bool? isArchived = null,
        [FromQuery] bool? isFeatured = null,
        [FromQuery] string? archived = null)
    {
        var includeAll = string.Equals(archived, "all", StringComparison.OrdinalIgnoreCase);
        var filter = new AdminProductFilterRequest(
            search,
            categoryId,
            isAvailable,
            isArchived,
            isFeatured,
            includeAll);

        var products = await _productService.AdminListAsync(filter);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id)
    {
        var product = await _productService.GetByIdAsync(id, includeArchived: true);
        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var product = await _productService.AdminCreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var product = await _productService.AdminUpdateAsync(id, request);
        return Ok(product);
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<IActionResult> SetAvailability(Guid id, [FromBody] SetAvailabilityRequest request)
    {
        await _productService.SetAvailabilityAsync(id, request.IsAvailable);
        return NoContent();
    }

    [HttpPatch("{id:guid}/featured")]
    public async Task<IActionResult> SetFeatured(Guid id, [FromBody] SetFeaturedRequest request)
    {
        await _productService.SetFeaturedAsync(id, request.IsFeatured);
        return NoContent();
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<ActionResult<ProductDto>> Archive(Guid id)
    {
        var product = await _productService.ArchiveAsync(id);
        return Ok(product);
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<ActionResult<ProductDto>> Restore(Guid id)
    {
        var product = await _productService.RestoreAsync(id);
        return Ok(product);
    }

    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(ProductImageLimits.MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProductImageLimits.MaxFileSizeBytes)]
    public async Task<ActionResult<ProductImageDto>> UploadImage(
        Guid id,
        IFormFile file,
        [FromQuery] bool isPrimary = false,
        [FromForm] string? altText = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Arquivo não enviado", Detail = "Selecione uma imagem." });

        if (file.Length > ProductImageLimits.MaxFileSizeBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Title = "Arquivo muito grande",
                Detail = "Imagem excede o tamanho máximo de 5 MB.",
                Status = StatusCodes.Status413PayloadTooLarge
            });

        await using var stream = file.OpenReadStream();
        var image = await _productService.AddImageAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            isPrimary,
            altText,
            cancellationToken);

        return Ok(image);
    }

    [HttpPatch("{id:guid}/images/{imageId:guid}")]
    public async Task<ActionResult<ProductImageDto>> UpdateImage(
        Guid id,
        Guid imageId,
        [FromBody] UpdateProductImageRequest request)
    {
        var image = await _productService.UpdateImageAsync(id, imageId, request);
        return Ok(image);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        await _productService.DeleteImageAsync(id, imageId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/images/order")]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> ReorderImages(
        Guid id,
        [FromBody] ReorderProductImagesRequest request)
    {
        var validation = await _reorderValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var images = await _productService.ReorderImagesAsync(id, request);
        return Ok(images);
    }
}

public record SetAvailabilityRequest(bool IsAvailable);
public record SetFeaturedRequest(bool IsFeatured);
