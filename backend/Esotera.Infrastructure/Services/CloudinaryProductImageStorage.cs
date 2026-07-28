using System.Text;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Esotera.Infrastructure.Services;

public class CloudinaryProductImageStorage : IProductImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _options;
    private readonly ILogger<CloudinaryProductImageStorage> _logger;

    public CloudinaryProductImageStorage(
        IOptions<CloudinaryOptions> options,
        ILogger<CloudinaryProductImageStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Cloudinary não configurado. Defina CLOUDINARY_CLOUD_NAME, CLOUDINARY_API_KEY e CLOUDINARY_API_SECRET.");
        }

        var account = new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<ProductImageUploadResult> UploadAsync(
        Stream imageStream,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(imageStream, contentType, originalFileName);

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var publicId = $"{_options.ProductsFolder.TrimEnd('/')}/{Guid.NewGuid():N}";

        await using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        ValidateMagicBytes(buffer);
        buffer.Position = 0;

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid():N}{extension}", buffer),
            PublicId = publicId,
            Overwrite = false,
            UniqueFilename = false,
            UseFilename = false,
            Folder = null // já incluído no PublicId
        };

        try
        {
            var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
            var secureUrl = result.SecureUrl?.ToString();
            if (result.Error != null || string.IsNullOrWhiteSpace(secureUrl))
            {
                _logger.LogError("Falha no upload Cloudinary: {Error}", result.Error?.Message ?? "sem URL");
                throw new ValidationException("file", "Não foi possível enviar a imagem. Tente novamente.");
            }

            return new ProductImageUploadResult(secureUrl, result.PublicId);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar imagem ao Cloudinary");
            throw new ValidationException("file", "Não foi possível enviar a imagem. Tente novamente.");
        }
    }

    public async Task DeleteAsync(string? publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            return;

        try
        {
            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            });

            if (result.Result is not ("ok" or "not found"))
            {
                _logger.LogWarning("Limpeza Cloudinary incompleta para publicId (omitido). Result={Result}", result.Result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover imagem órfã no Cloudinary (limpeza pendente)");
        }
    }

    private static void ValidateUpload(Stream imageStream, string contentType, string originalFileName)
    {
        if (imageStream == null || !imageStream.CanRead)
            throw new ValidationException("file", "Arquivo de imagem inválido.");

        if (imageStream.CanSeek && imageStream.Length == 0)
            throw new ValidationException("file", "Arquivo vazio.");

        if (imageStream.CanSeek && imageStream.Length > ProductImageLimits.MaxFileSizeBytes)
            throw new ValidationException("file", "Imagem excede o tamanho máximo de 5 MB.");

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
