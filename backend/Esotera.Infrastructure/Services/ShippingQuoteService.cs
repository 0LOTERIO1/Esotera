using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Adaptador legado síncrono sobre <see cref="IShippingOptionsService"/>.
/// Novos fluxos devem usar GetAvailableOptionsAsync / RequireOptionAsync.
/// </summary>
public sealed class ShippingQuoteService : IShippingQuoteService, ISimulatedShippingService
{
    private readonly IShippingOptionsService _options;

    public ShippingQuoteService(IShippingOptionsService options)
    {
        _options = options;
    }

    public (decimal Price, int EstimatedDays) Quote(
        string shippingMethodId,
        string cep,
        string state,
        decimal productsTotalAfterDiscount,
        StoreSettings settings)
    {
        var digits = new string(cep.Where(char.IsDigit).ToArray());
        var query = new ShippingQuoteQuery(digits, state, productsTotalAfterDiscount);
        var option = _options
            .RequireOptionAsync(shippingMethodId, query, settings)
            .GetAwaiter()
            .GetResult();

        return (option.FinalPrice, option.EstimatedDaysMax);
    }
}
