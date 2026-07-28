namespace Esotera.Domain.Entities;

public class OrderStatusHistory
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
