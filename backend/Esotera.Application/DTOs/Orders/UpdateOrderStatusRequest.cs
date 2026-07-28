namespace Esotera.Application.DTOs.Orders;

public record UpdateOrderStatusRequest(string Status, string? Note, long? ExpectedVersion = null);

public record OrderFilterRequest(
    string? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20
);
