using System.Collections.Concurrent;
using System.Text;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Storage offline para testes e fallback local sem Cloudinary.
/// Não persiste bytes; gera URLs/publicIds determinísticos em memória.
/// </summary>
public class FakeProductImageStorage : IProductImageStorage
{
    private readonly ConcurrentDictionary<string, byte> _stored = new();
    private readonly List<string> _deleted = new();
    private readonly object _gate = new();

    public bool ThrowOnUpload { get; set; }
    public bool ThrowOnDelete { get; set; }
    public Func<Task>? AfterUploadHook { get; set; }
    public IReadOnlyList<string> DeletedPublicIds
    {
        get { lock (_gate) return _deleted.ToList(); }
    }

    public int UploadCount { get; private set; }

    public async Task<ProductImageUploadResult> UploadAsync(
        Stream imageStream,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnUpload)
            throw new ValidationException("file", "Falha simulada no armazenamento de imagem.");

        ValidateUpload(imageStream, contentType, originalFileName);

        await using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
            throw new ValidationException("file", "Arquivo vazio.");
        if (buffer.Length > ProductImageLimits.MaxFileSizeBytes)
            throw new ValidationException("file", "Imagem excede o tamanho máximo de 5 MB.");

        buffer.Position = 0;
        ValidateMagicBytes(buffer);

        UploadCount++;
        var publicId = $"esotera/products/fake-{Guid.NewGuid():N}";
        var url = $"https://res.cloudinary.com/esotera-test/image/upload/{publicId}.jpg";
        _stored[publicId] = 1;

        if (AfterUploadHook != null)
            await AfterUploadHook();

        return new ProductImageUploadResult(url, publicId);
    }

    public Task DeleteAsync(string? publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            return Task.CompletedTask;

        if (ThrowOnDelete)
            throw new InvalidOperationException("Falha simulada ao remover imagem.");

        _stored.TryRemove(publicId, out _);
        lock (_gate) _deleted.Add(publicId);
        return Task.CompletedTask;
    }

    public bool WasUploaded(string publicId) => _stored.ContainsKey(publicId);

    private static void ValidateUpload(Stream imageStream, string contentType, string originalFileName)
    {
        if (imageStream == null || !imageStream.CanRead)
            throw new ValidationException("file", "Arquivo de imagem inválido.");

        if (string.IsNullOrWhiteSpace(contentType) ||
            !ProductImageLimits.AllowedContentTypes.Contains(contentType))
        {
            throw new ValidationException("file", "Tipo de imagem não suportado. Use JPEG, PNG ou WebP.");
        }

        var extension = Path.GetExtension(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension) ||
            !ProductImageLimits.AllowedExtensions.Contains(extension))
        {
            throw new ValidationException("file", "Extensão de arquivo não permitida.");
        }
    }

    private static void ValidateMagicBytes(Stream stream)
    {
        Span<byte> header = stackalloc byte[12];
        var read = stream.Read(header);
        stream.Position = 0;

        if (read < 3)
            throw new ValidationException("file", "Arquivo de imagem inválido.");

        if (header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return;
        if (read >= 12 &&
            Encoding.ASCII.GetString(header[..4]) == "RIFF" &&
            Encoding.ASCII.GetString(header.Slice(8, 4)) == "WEBP")
            return;

        throw new ValidationException("file", "Conteúdo do arquivo não corresponde a uma imagem válida.");
    }
}
