namespace Esotera.Domain.Entities;

public class CouponUsage
{
    public Guid Id { get; set; }
    public Guid CouponId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public DateTime UsedAtUtc { get; set; }

    public Coupon Coupon { get; set; } = null!;
}
