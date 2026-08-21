using Esotera.Application.DTOs.Common;

namespace Esotera.Application.DTOs.J3;

public record J3FulfillmentFilterRequest(
    string? Status,
    Guid? OrderId,
    string? TrackingNumber,
    int Page = 1,
    int PageSize = 20);

/// <summary>Listagem admin — sem PII, token ou payload GraphQL.</summary>
public sealed record J3FulfillmentAdminListItemDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Status,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    int AttemptCount,
    string? LastErrorCode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanRetrySafely,
    bool NeedsManualReview,
    bool IsPossiblyStuck);

/// <summary>Detalhe admin diagnóstico. Sem endereço, telefone, e-mail, token, ChNFe completa ou raw error.</summary>
public sealed record J3FulfillmentAdminDetailDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string ShippingMethodId,
    string OrderStatus,
    string PaymentStatus,
    string Status,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    string? J3DeliveryPointId,
    int AttemptCount,
    string? LastErrorCode,
    DateTime? LastErrorAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanRetrySafely,
    bool NeedsManualReview,
    bool IsPossiblyStuck,
    bool CanSendToJ3,
    string EligibilityReason);
