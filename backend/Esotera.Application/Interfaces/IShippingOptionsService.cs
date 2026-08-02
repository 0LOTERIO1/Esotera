using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Cotação centralizada (checkout + CreateOrder). Sem fallback fictício Melhor Envio.
/// </summary>
public interface IShippingOptionsService
{
    /// <summary>
    /// Retorna todas as opções disponíveis (J3 se elegível + ME se flag/token/API OK).
    /// </summary>
    Task<IReadOnlyList<NormalizedShippingOption>> GetAvailableOptionsAsync(
        ShippingQuoteQuery query,
        StoreSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recota e exige o método indicado. Lança ValidationException se indisponível.
    /// </summary>
    Task<NormalizedShippingOption> RequireOptionAsync(
        string shippingMethodId,
        ShippingQuoteQuery query,
        StoreSettings settings,
        CancellationToken cancellationToken = default);
}
