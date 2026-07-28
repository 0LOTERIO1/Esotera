namespace Esotera.Application.Options;

public static class ProductImageLimits
{
    public const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public const int MaxImagesPerProduct = 8;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };
}
