namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Resposta segura da ação Admin processar J3. Sem token, XML, ChNFe, CPF ou endereço.
/// </summary>
public sealed record J3FulfillmentAdminProcessDto(
    Guid OrderId,
    Guid? FulfillmentId,
    string? OrderNumber,
    string Status,
    bool CanSendToJ3,
    string EligibilityReason,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    int AttemptCount,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    bool NeedsManualReview,
    bool Processed);
