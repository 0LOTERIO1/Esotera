using Esotera.Application.DTOs.Categories;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly EsoteraDbContext _context;

    public CategoryService(EsoteraDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync()
    {
        return await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync();
    }
}
