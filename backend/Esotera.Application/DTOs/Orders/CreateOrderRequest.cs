namespace Esotera.Application.DTOs.Orders;

public record CreateOrderRequest(
    CreateOrderItemRequest[] Items,
    OrderAddressInput? Address,
    Guid? AddressId,
    string ShippingMethodId,
    string PaymentMethod,
    int? Installments,
    string? CouponCode
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity,
    string? Variation
);

public record OrderAddressInput(
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    bool? IsResidentialAddress = null
);
