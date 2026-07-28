namespace Esotera.Domain.Entities;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    /// <summary>URL HTTPS (Cloudinary) ou caminho legado (/images/... ou /media/...).</summary>
    public string SecureUrl { get; set; } = string.Empty;
    /// <summary>PublicId Cloudinary; nulo para imagens legadas locais.</summary>
    public string? PublicId { get; set; }
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Product Product { get; set; } = null!;
}
