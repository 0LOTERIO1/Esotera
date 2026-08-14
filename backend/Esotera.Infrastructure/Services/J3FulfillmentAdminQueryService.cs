using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>Consultas admin J3Fulfillment. Sem IJ3FulfillmentClient / sem processor.</summary>
public sealed class J3FulfillmentAdminQueryService : IJ3FulfillmentAdminQueryService
{
    private readonly EsoteraDbContext _context;
    private readonly int _staleMinutes;

    public J3FulfillmentAdminQueryService(
        EsoteraDbContext context,
        IOptions<J3ShippingOptions> j3Options)
    {
        _context = context;
        _staleMinutes = j3Options.Value.ProcessingStaleMinutes > 0
            ? Math.Clamp(j3Options.Value.ProcessingStaleMinutes, 1, 24 * 60)
            : 15;
    }

    public async Task<PagedResult<J3FulfillmentAdminListItemDto>> ListAsync(
        J3FulfillmentFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);
        var now = DateTime.UtcNow;

        var query = _context.J3Fulfillments.AsNoTracking().Include(f => f.Order).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            if (!J3FulfillmentStatus.IsValid(status))
            {
                return new PagedResult<J3FulfillmentAdminListItemDto>([], 0, page, pageSize);
            }

            query = query.Where(f => f.Status == status);
        }

        if (filter.OrderId is { } orderId)
            query = query.Where(f => f.OrderId == orderId);

        if (!string.IsNullOrWhiteSpace(filter.TrackingNumber))
        {
            var tracking = filter.TrackingNumber.Trim();
            query = query.Where(f => f.J3TrackingNumber == tracking);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(f => f.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(f => MapList(f, now)).ToList();
        return new PagedResult<J3FulfillmentAdminListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<J3FulfillmentAdminDetailDto?> GetAsync(
        Guid fulfillmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.J3Fulfillments.AsNoTracking()
            .Include(f => f.Order)
            .FirstOrDefaultAsync(f => f.Id == fulfillmentId, cancellationToken);
        if (row is null)
            return null;

        return MapDetail(row, DateTime.UtcNow);
    }

    private J3FulfillmentAdminListItemDto MapList(Domain.Entities.J3Fulfillment f, DateTime now)
    {
        var stuck = J3FulfillmentAdminFlags.IsPossiblyStuck(f.Status, f.UpdatedAtUtc, now, _staleMinutes);
        return new J3FulfillmentAdminListItemDto(
            f.Id,
            f.OrderId,
            f.Order.OrderNumber,
            f.Status,
            f.J3OrderId,
            f.J3OrderCode,
            f.J3TrackingNumber,
            f.AttemptCount,
            f.LastErrorCode,
            f.CreatedAtUtc,
            f.UpdatedAtUtc,
            f.CompletedAtUtc,
            J3FulfillmentAdminFlags.CanRetrySafely(f.Status),
            J3FulfillmentAdminFlags.NeedsManualReview(f.Status, stuck),
            stuck);
    }

    private J3FulfillmentAdminDetailDto MapDetail(Domain.Entities.J3Fulfillment f, DateTime now)
    {
        var stuck = J3FulfillmentAdminFlags.IsPossiblyStuck(f.Status, f.UpdatedAtUtc, now, _staleMinutes);
        return new J3FulfillmentAdminDetailDto(
            f.Id,
            f.OrderId,
            f.Order.OrderNumber,
            f.Order.ShippingMethodId,
            f.Order.Status,
            f.Order.PaymentStatus,
            f.Status,
            f.J3OrderId,
            f.J3OrderCode,
            f.J3TrackingNumber,
            f.J3DeliveryPointId,
            f.AttemptCount,
            f.LastErrorCode,
            f.LastErrorAtUtc,
            f.CreatedAtUtc,
            f.UpdatedAtUtc,
            f.CompletedAtUtc,
            J3FulfillmentAdminFlags.CanRetrySafely(f.Status),
            J3FulfillmentAdminFlags.NeedsManualReview(f.Status, stuck),
            stuck);
    }
}
