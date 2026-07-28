using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Interfaces;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class AdminQueryService : IAdminQueryService
{
    private readonly EsoteraDbContext _context;

    public AdminQueryService(EsoteraDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync()
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.CustomerName,
                o.CreatedAtUtc
            })
            .ToListAsync();

        var nonCancelled = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var statusCounts = orders
            .GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        int Count(string status) =>
            statusCounts.TryGetValue(status, out var c) ? c : 0;

        var availableProducts = await _context.Products.CountAsync(p => p.IsAvailable);

        var customersWithOrders = await _context.Orders
            .Select(o => o.UserId)
            .Distinct()
            .CountAsync();

        var recent = orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(8)
            .Select(o => new AdminRecentOrderDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.CustomerName,
                o.CreatedAtUtc
            ))
            .ToArray();

        var topProducts = await GetSoldProductsAsync();

        return new AdminDashboardDto(
            orders.Count,
            nonCancelled.Sum(o => o.Total),
            Count(OrderStatus.AwaitingPayment),
            Count(OrderStatus.PaymentApproved),
            Count(OrderStatus.Preparing),
            Count(OrderStatus.Shipped),
            Count(OrderStatus.Delivered),
            Count(OrderStatus.Cancelled),
            availableProducts,
            customersWithOrders,
            recent,
            topProducts.Take(10).ToArray()
        );
    }

    public async Task<PagedResult<AdminOrderSummaryDto>> ListOrdersAsync(OrderFilterRequest filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);

        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(o => o.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search) ||
                o.CustomerName.ToLower().Contains(search) ||
                o.CustomerEmail.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.Items.Count,
                o.CustomerName,
                o.PaymentMethod,
                o.ShippingMethodName,
                o.CreatedAtUtc,
                o.RowVersion
            ))
            .ToListAsync();

        return new PagedResult<AdminOrderSummaryDto>(items, totalCount, page, pageSize);
    }

    public async Task<AdminOrderDetailDto?> GetOrderAsync(Guid orderId)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order == null ? null : MapDetail(order);
    }

    public async Task<IReadOnlyList<AdminSoldProductDto>> GetSoldProductsAsync()
    {
        // Snapshot congelado; materializa antes do GroupBy (compatível com InMemory)
        var items = await _context.OrderItems
            .AsNoTracking()
            .Where(i => i.Order.Status != OrderStatus.Cancelled)
            .Select(i => new
            {
                i.ProductId,
                i.ProductName,
                i.ImageUrl,
                i.Quantity,
                i.LineTotal,
                i.OrderId
            })
            .ToListAsync();

        return items
            .GroupBy(i => new { i.ProductId, i.ProductName, i.ImageUrl })
            .Select(g => new AdminSoldProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.ImageUrl,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.LineTotal),
                g.Select(x => x.OrderId).Distinct().Count()
            ))
            .OrderByDescending(x => x.QuantitySold)
            .ThenByDescending(x => x.TotalRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<AdminCustomerDto>> ListCustomersAsync()
    {
        var rows = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled)
            .Select(o => new
            {
                o.UserId,
                o.CustomerName,
                o.CustomerEmail,
                o.CustomerPhone,
                o.Total,
                o.CreatedAtUtc
            })
            .ToListAsync();

        return rows
            .GroupBy(o => o.UserId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.CreatedAtUtc).First();
                return new AdminCustomerDto(
                    g.Key,
                    latest.CustomerName,
                    latest.CustomerEmail,
                    latest.CustomerPhone,
                    g.Count(),
                    g.Sum(o => o.Total),
                    g.Max(o => o.CreatedAtUtc)
                );
            })
            .OrderByDescending(c => c.LastOrderAt)
            .ToList();
    }

    private static AdminOrderDetailDto MapDetail(Domain.Entities.Order order) => new(
        order.Id,
        order.OrderNumber,
        order.Status,
        order.Subtotal,
        order.Discount,
        order.ShippingPrice,
        order.Total,
        order.CouponCode,
        new AdminOrderShippingDto(
            order.ShippingMethodId,
            order.ShippingMethodName,
            order.ShippingProvider,
            order.ShippingEstimatedDays
        ),
        new AdminOrderPaymentDto(
            order.PaymentMethod,
            order.PaymentInstallments,
            order.PaymentStatus
        ),
        new AdminOrderCustomerDto(
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone
        ),
        new AdminOrderAddressDto(
            order.ShipCep,
            order.ShipStreet,
            order.ShipNumber,
            order.ShipComplement,
            order.ShipNeighborhood,
            order.ShipCity,
            order.ShipState
        ),
        order.Items.Select(i => new AdminOrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductName,
            i.UnitPrice,
            i.Quantity,
            i.Variation,
            i.ImageUrl,
            i.LineTotal
        )).ToArray(),
        order.StatusHistory.OrderBy(h => h.CreatedAtUtc).Select(h => new AdminOrderStatusHistoryDto(
            h.FromStatus,
            h.ToStatus,
            h.Note,
            h.CreatedAtUtc
        )).ToArray(),
        order.CreatedAtUtc,
        order.UpdatedAtUtc,
        order.RowVersion
    );
}
