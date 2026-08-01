using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Contrato de cotação de frete usado no checkout/pedidos.
/// Implementações de transportadora real devem falhar de forma segura
/// (sem inventar preços) e permitir fallback para simulação até o go-live.
/// </summary>
public interface IShippingQuoteService
{
    (decimal Price, int EstimatedDays) Quote(
        string shippingMethodId,
        string cep,
        string state,
        decimal productsTotalAfterDiscount,
        StoreSettings settings);
}
