namespace Esotera.Application.DTOs.Admin;

public record AdminDashboardDto(
    int TotalOrders,
    decimal TotalSales,
    int AwaitingPayment,
    int PaymentApproved,
    int Preparing,
    int Shipped,
    int Delivered,
    int Cancelled,
    int AvailableProducts,
    int CustomersWithOrders,
    AdminRecentOrderDto[] RecentOrders,
    AdminSoldProductDto[] TopProducts
);

public record AdminRecentOrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Total,
    string CustomerName,
    DateTime CreatedAt
);

public record AdminSoldProductDto(
    Guid? ProductId,
    string ProductName,
    string? ImageUrl,
    int QuantitySold,
    decimal TotalRevenue,
    int OrderCount
);

public record AdminCustomerDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    int OrderCount,
    decimal TotalSpent,
    DateTime? LastOrderAt
);

public record AdminOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Total,
    int ItemCount,
    string CustomerName,
    string PaymentMethod,
    string ShippingMethodName,
    DateTime CreatedAt,
    long RowVersion,
    /// <summary>Serviço realmente cotado (ex.: SEDEX, PAC). Null em pedidos antigos.</summary>
    string? ShippingServiceName = null,
    /// <summary>Transportadora do serviço cotado (ex.: Correios). Null em pedidos antigos.</summary>
    string? ShippingCarrierName = null
);

public record AdminOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingPrice,
    decimal Total,
    string? CouponCode,
    AdminOrderShippingDto Shipping,
    AdminOrderPaymentDto Payment,
    AdminOrderCustomerDto Customer,
    AdminOrderAddressDto Address,
    AdminOrderItemDto[] Items,
    AdminOrderStatusHistoryDto[] StatusHistory,
    AdminOrderFiscalSummaryDto Fiscal,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion,
    /// <summary>Envio Melhor Envio. Null quando o frete não é Melhor Envio ou ainda não há registro.</summary>
    AdminOrderMelhorEnvioDto? MelhorEnvio = null
);

/// <summary>
/// Envio Melhor Envio exposto ao Admin. Sem token, sem payload bruto, sem chave de NF-e.
/// </summary>
public record AdminOrderMelhorEnvioDto(
    string Status,
    string Environment,
    string? ShipmentId,
    string? Protocol,
    string? TrackingCode,
    string? TrackingUrl,
    string? LabelUrl,
    DateTime? CartCreatedAtUtc,
    DateTime? PurchasedAtUtc,
    DateTime? LabelGeneratedAtUtc,
    DateTime? LastSyncAtUtc,
    string? LastSyncErrorCode,
    string? LastSyncErrorMessage,
    DateTime UpdatedAtUtc
);

/// <summary>
/// Snapshot da cotação gravado no pedido. Campos opcionais são null em pedidos
/// anteriores à captura estruturada da cotação.
/// </summary>
public record AdminOrderShippingDto(
    string MethodId,
    string MethodName,
    string Provider,
    int? EstimatedDays,
    /// <summary>Transportadora (ex.: Correios).</summary>
    string? CarrierName = null,
    /// <summary>Serviço realmente cotado (ex.: SEDEX, PAC).</summary>
    string? ServiceName = null,
    /// <summary>Id do serviço na transportadora/marketplace de frete.</summary>
    int? ServiceId = null,
    /// <summary>Preço cotado pela transportadora, antes de frete grátis/subsídio.</summary>
    decimal? OriginalPrice = null,
    int? DeliveryMinDays = null,
    int? DeliveryMaxDays = null,
    /// <summary>Ambiente em que a cotação foi feita (sandbox/production).</summary>
    string? QuoteEnvironment = null,
    DateTime? QuotedAtUtc = null,
    bool? FreeShippingApplied = null,
    bool? SubsidyApplied = null
);

public record AdminOrderPaymentDto(
    string Method,
    int? Installments,
    string Status
);

public record AdminOrderCustomerDto(
    string Name,
    string Email,
    string? Phone
);

public record AdminOrderAddressDto(
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State
);

public record AdminOrderItemDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? Variation,
    string? Sku,
    string? ImageUrl,
    decimal LineTotal
);

public record AdminOrderStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTime CreatedAt
);
