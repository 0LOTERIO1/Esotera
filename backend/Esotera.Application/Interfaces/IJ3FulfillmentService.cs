namespace Esotera.Application.Interfaces;

/// <summary>
/// Persistência/claim local de fulfillment J3 (Passo 4.1).
/// ZERO chamadas HTTP J3. Sem createTmsOrder / stamp / BackgroundService / auto-retry.
/// </summary>
public interface IJ3FulfillmentService
{
    /// <summary>
    /// Garante registro Pending 1:1 se payment_approved + ShippingMethodId=j3.
    /// Independente de J3_ENABLED e J3_FULFILLMENT_ENABLED (obrigação local durável).
    /// Idempotente via unique OrderId. Zero HTTP.
    /// Auditoria futura: payment_approved + j3 + fulfillment ausente (sem worker neste passo).
    /// </summary>
    Task EnsurePendingAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim atômico Pending → Processing. Retorna true se este caller ganhou o claim (rows==1).
    /// Termina antes de qualquer HTTP externo futuro. Created/UnknownOutcome → false.
    /// </summary>
    Task<bool> TryClaimPendingAsync(Guid fulfillmentId, CancellationToken cancellationToken = default);
}
