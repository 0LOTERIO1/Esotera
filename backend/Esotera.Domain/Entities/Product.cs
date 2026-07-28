namespace Esotera.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public string? FeaturesJson { get; set; }
    public string? PackageContentsJson { get; set; }
    public string? VariationsJson { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public bool IsDemo { get; set; }
    public long RowVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
