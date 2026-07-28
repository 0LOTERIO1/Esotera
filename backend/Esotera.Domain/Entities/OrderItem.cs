namespace Esotera.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Variation { get; set; }
    public string? ImageUrl { get; set; }
    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;
}
