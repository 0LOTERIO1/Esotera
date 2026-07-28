using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Orders;

namespace Esotera.Application.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(Guid userId, CreateOrderRequest request, string idempotencyKey);
    Task<IReadOnlyList<OrderListDto>> ListMineAsync(Guid userId);
    Task<OrderDto?> GetMineAsync(Guid userId, Guid orderId);
    Task<PagedResult<OrderListDto>> AdminListAsync(OrderFilterRequest filter);
    Task<OrderDto?> AdminGetAsync(Guid orderId);
    Task<OrderDto> UpdateStatusAsync(Guid orderId, UpdateOrderStatusRequest request, Guid changedByUserId);
}
