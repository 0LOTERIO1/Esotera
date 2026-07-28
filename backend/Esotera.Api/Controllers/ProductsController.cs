using Esotera.Application.DTOs.Products;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductListDto>>> List()
    {
        var products = await _productService.ListAsync(availableOnly: true);
        return Ok(products);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDto>> GetBySlug(string slug)
    {
        var product = await _productService.GetBySlugAsync(slug);
        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [HttpGet("id/{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
            return NotFound();

        return Ok(product);
    }
}
