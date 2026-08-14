namespace Esotera.Application.DTOs.Orders;

public record OrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal ShippingPrice,
    decimal Total,
    string? CouponCode,
    OrderShippingDto Shipping,
    OrderPaymentDto Payment,
    OrderCustomerDto Customer,
    OrderAddressDto Address,
    OrderItemDto[] Items,
    OrderStatusHistoryDto[] StatusHistory,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long RowVersion = 0
);

public record OrderListDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Total,
    int ItemCount,
    string CustomerName,
    DateTime CreatedAt
);

public record OrderShippingDto(
    string MethodId,
    string MethodName,
    string Provider,
    int? EstimatedDays
);

public record OrderPaymentDto(
    string Method,
    int? Installments,
    string Status
);

public record OrderCustomerDto(
    string Name,
    string Email,
    string? Phone,
    string? Cpf
);

public record OrderAddressDto(
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State
);

public record OrderItemDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string? Variation,
    string? ImageUrl,
    decimal LineTotal
);

public record OrderStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTime CreatedAt
);
