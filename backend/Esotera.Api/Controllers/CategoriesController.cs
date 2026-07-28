using Esotera.Application.DTOs.Categories;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List()
    {
        var categories = await _categoryService.ListAsync();
        return Ok(categories);
    }
}
