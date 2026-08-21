using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Gate local J3: Order + FiscalInvoice + fulfillment. Zero HTTP / zero XmlCipher.
/// </summary>
public interface IJ3FulfillmentEligibilityService
{
    J3FulfillmentEligibilityResult Evaluate(
        Order? order,
        J3FiscalEligibilitySnapshot? fiscal,
        J3Fulfillment? fulfillment,
        bool fulfillmentEnabled);

    /// <summary>
    /// Carrega Order, snapshot fiscal (sem XmlCipher) e J3Fulfillment opcional.
    /// </summary>
    Task<J3FulfillmentEligibilityResult> EvaluateForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
