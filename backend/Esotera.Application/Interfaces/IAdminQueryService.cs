using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Orders;

namespace Esotera.Application.Interfaces;

public interface IAdminQueryService
{
    Task<AdminDashboardDto> GetDashboardAsync();
    Task<PagedResult<AdminOrderSummaryDto>> ListOrdersAsync(OrderFilterRequest filter);
    Task<AdminOrderDetailDto?> GetOrderAsync(Guid orderId);
    Task<IReadOnlyList<AdminSoldProductDto>> GetSoldProductsAsync();
    Task<IReadOnlyList<AdminCustomerDto>> ListCustomersAsync();
}
