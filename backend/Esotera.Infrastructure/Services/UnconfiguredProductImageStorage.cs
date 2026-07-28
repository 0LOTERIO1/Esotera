using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Usado quando Cloudinary não está configurado: a API sobe, mas uploads falham com mensagem clara.
/// </summary>
public class UnconfiguredProductImageStorage : IProductImageStorage
{
    public Task<ProductImageUploadResult> UploadAsync(
        Stream imageStream,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        throw new ValidationException(
            "file",
            "Armazenamento de imagens não configurado. Defina as variáveis CLOUDINARY_* no backend.");
    }

    public Task DeleteAsync(string? publicId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
