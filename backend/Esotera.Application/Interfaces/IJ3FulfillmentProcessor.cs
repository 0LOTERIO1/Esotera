using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Processor unitário: claim Pending→Processing e uma chamada a <see cref="IJ3FulfillmentClient"/>.
/// Sem BackgroundService, sem webhook, sem retry automático.
/// Gate: somente <c>J3_FULFILLMENT_ENABLED</c> (não exige <c>J3_ENABLED</c>).
/// </summary>
public interface IJ3FulfillmentProcessor
{
    /// <summary>
    /// Processa um J3Fulfillment Pending. Idempotente para Created/UnknownOutcome/RetryableFailure/Processing.
    /// UnknownOutcome nunca reprocessa. RetryableFailure não reprocessa neste passo.
    /// </summary>
    Task ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default);

    /// <summary>Leitura sanitizada para observabilidade futura (sem PII de endereço/telefone/token).</summary>
    Task<J3FulfillmentAdminDto?> GetSnapshotAsync(Guid fulfillmentId, CancellationToken cancellationToken = default);
}
