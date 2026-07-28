namespace Esotera.Domain.Entities;

public class Coupon
{
    public Guid Id { get; set; }
    /// <summary>Sempre normalizado: Trim + UpperInvariant.</summary>
    public string Code { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public decimal MinPurchase { get; set; }
    public bool AppliesToShipping { get; set; }
    public bool OneUsePerCustomer { get; set; } = true;
    /// <summary>null = ilimitado globalmente.</summary>
    public int? MaxTotalUses { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<CouponUsage> Usages { get; set; } = new List<CouponUsage>();
}
