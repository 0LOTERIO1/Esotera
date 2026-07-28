namespace Esotera.Application.Interfaces;

public record ProductImageUploadResult(string SecureUrl, string PublicId);

/// <summary>
/// Abstração de armazenamento de imagens de produto (Cloudinary em produção; fake em testes).
/// </summary>
public interface IProductImageStorage
{
    Task<ProductImageUploadResult> UploadAsync(
        Stream imageStream,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Remove pelo publicId. Imagens legadas sem publicId são ignoradas com segurança.</summary>
    Task DeleteAsync(string? publicId, CancellationToken cancellationToken = default);
}
