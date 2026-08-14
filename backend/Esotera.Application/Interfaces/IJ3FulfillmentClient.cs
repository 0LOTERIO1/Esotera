using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Cliente GraphQL J3 Flex — somente mutations de fulfillment.
/// Separado de <see cref="IJ3Client"/> (coverage/tracking read-only).
/// Passo 4.2: contrato + HTTP preparado. ZERO caller em PaymentService / J3FulfillmentService / worker.
/// </summary>
public interface IJ3FulfillmentClient
{
    /// <summary>
    /// Prepara e envia <c>createTmsOrders</c> (array de exatamente 1 input) no máximo uma vez.
    /// Exige <c>J3_FULFILLMENT_ENABLED</c> (não exige <c>J3_ENABLED</c> — pedidos já pagos).
    /// Caso contrário não envia HTTP.
    /// Não gera etiqueta. Sem retry automático.
    /// </summary>
    Task<J3CreateOrderAttemptResult> CreateOrderAsync(
        Order order,
        StoreSettings settings,
        CancellationToken cancellationToken = default);
}
