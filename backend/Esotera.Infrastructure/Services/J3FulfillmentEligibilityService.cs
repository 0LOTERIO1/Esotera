using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Gate local J3. Lê Status/ChNFe/Number/Series/AuthorizedAtUtc — nunca XmlCipher.
/// </summary>
public sealed class J3FulfillmentEligibilityService : IJ3FulfillmentEligibilityService
{
    private readonly EsoteraDbContext _context;
    private readonly J3ShippingOptions _j3;

    public J3FulfillmentEligibilityService(
        EsoteraDbContext context,
        IOptions<J3ShippingOptions> j3Options)
    {
        _context = context;
        _j3 = j3Options.Value;
    }

    public J3FulfillmentEligibilityResult Evaluate(
        Order? order,
        J3FiscalEligibilitySnapshot? fiscal,
        J3Fulfillment? fulfillment,
        bool fulfillmentEnabled) =>
        J3FulfillmentEligibility.Evaluate(order, fiscal, fulfillment, fulfillmentEnabled);

    public async Task<J3FulfillmentEligibilityResult> EvaluateForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        var fiscalRow = await _context.FiscalInvoices.AsNoTracking()
            .Where(f => f.OrderId == orderId)
            .OrderByDescending(f => f.Status == FiscalInvoiceStatus.Authorized)
            .ThenByDescending(f => f.UpdatedAtUtc)
            .Select(f => new
            {
                f.Status,
                f.ChNFe,
                f.Number,
                f.Series,
                f.AuthorizedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        J3FiscalEligibilitySnapshot? fiscal = fiscalRow is null
            ? null
            : new J3FiscalEligibilitySnapshot
            {
                Status = fiscalRow.Status,
                ChNFe = fiscalRow.ChNFe,
                Number = fiscalRow.Number,
                Series = fiscalRow.Series,
                AuthorizedAtUtc = fiscalRow.AuthorizedAtUtc
            };

        var fulfillment = await _context.J3Fulfillments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.OrderId == orderId, cancellationToken);

        return Evaluate(order, fiscal, fulfillment, _j3.FulfillmentEnabled);
    }
}
