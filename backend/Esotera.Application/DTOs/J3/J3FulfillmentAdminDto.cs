namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Snapshot admin de fulfillment J3. Sem token, endereço ou telefone.
/// </summary>
public sealed record J3FulfillmentAdminDto(
    Guid Id,
    Guid OrderId,
    string Status,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    string? J3DeliveryPointId,
    int AttemptCount,
    string? LastErrorCode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
