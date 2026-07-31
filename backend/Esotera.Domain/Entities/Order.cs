namespace Esotera.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingPrice { get; set; }
    public decimal Total { get; set; }

    public string? CouponCode { get; set; }

    // Snapshots de cupom (nullable em pedidos legados)
    public Guid? CouponId { get; set; }
    public decimal? CouponNominalDiscount { get; set; }
    public decimal? CouponMinPurchaseSnapshot { get; set; }
    public decimal? CouponDiscountApplied { get; set; }

    // Snapshots de configurações / frete no momento do pedido
    public decimal? FreeShippingMinSnapshot { get; set; }
    public string? FreeShippingStatesSnapshot { get; set; }
    public decimal? J3PriceSnapshot { get; set; }
    public int? J3CutoffHourSnapshot { get; set; }
    public bool? ShippingSubsidyEnabledSnapshot { get; set; }
    public decimal? ShippingSubsidyAmountSnapshot { get; set; }

    public string ShippingMethodId { get; set; } = string.Empty;
    public string ShippingMethodName { get; set; } = string.Empty;
    public string ShippingProvider { get; set; } = string.Empty;
    public int ShippingEstimatedDays { get; set; }

    public string ShipCep { get; set; } = string.Empty;
    public string ShipStreet { get; set; } = string.Empty;
    public string ShipNumber { get; set; } = string.Empty;
    public string? ShipComplement { get; set; }
    public string ShipNeighborhood { get; set; } = string.Empty;
    public string ShipCity { get; set; } = string.Empty;
    public string ShipState { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;
    public int? PaymentInstallments { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;

    /// <summary>ID da order no Mercado Pago (ORD…). Preferencial para webhook/consulta.</summary>
    public string? MercadoPagoOrderId { get; set; }
    /// <summary>ID do pagamento interno na order (PAY…) ou legado Payments API.</summary>
    public string? MercadoPagoPaymentId { get; set; }
    /// <summary>Último status reportado pelo MP (action_required, processed, approved…).</summary>
    public string? MercadoPagoPaymentStatus { get; set; }
    /// <summary>Idempotência da criação do pagamento no MP.</summary>
    public string? PaymentIdempotencyKey { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerCpf { get; set; }

    public string? IdempotencyKey { get; set; }
    public string? IdempotencyFingerprint { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long RowVersion { get; set; }

    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
