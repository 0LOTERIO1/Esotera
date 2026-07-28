using Esotera.Application.DTOs.Categories;

namespace Esotera.Application.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync();
}
