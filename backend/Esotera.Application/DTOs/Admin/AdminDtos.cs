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
    long RowVersion
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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion
);

public record AdminOrderShippingDto(
    string MethodId,
    string MethodName,
    string Provider,
    int EstimatedDays
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
    string? ImageUrl,
    decimal LineTotal
);

public record AdminOrderStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTime CreatedAt
);
