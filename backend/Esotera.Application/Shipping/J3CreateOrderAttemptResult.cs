namespace Esotera.Application.Shipping;

/// <summary>
/// Resultado conservador de uma tentativa de createTmsOrder.
/// O caller futuro distingue created / definite failure / unknown sem interpretar texto.
/// Não expõe raw GraphQL / HTTP body.
/// </summary>
public sealed class J3CreateOrderAttemptResult
{
    public required J3CreateOrderOutcome Outcome { get; init; }

    /// <summary>ID da ordem na J3 — só em Success inequívoco (e opcionalmente ecoado em Unknown).</summary>
    public string? OrderId { get; init; }

    public string? OrderCode { get; init; }
    public string? TrackingNumber { get; init; }
    public string? DeliveryPointId { get; init; }

    /// <summary>Código sanitizado (sem PII). Ausente em Success.</summary>
    public string? ErrorCode { get; init; }

    public static J3CreateOrderAttemptResult Success(
        string orderId,
        string? orderCode,
        string? trackingNumber,
        string? deliveryPointId) =>
        new()
        {
            Outcome = J3CreateOrderOutcome.Success,
            OrderId = orderId,
            OrderCode = orderCode,
            TrackingNumber = trackingNumber,
            DeliveryPointId = deliveryPointId
        };

    public static J3CreateOrderAttemptResult DefiniteFailure(string errorCode) =>
        new()
        {
            Outcome = J3CreateOrderOutcome.DefiniteFailure,
            ErrorCode = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Configuration
        };

    public static J3CreateOrderAttemptResult Unknown(string errorCode) =>
        new()
        {
            Outcome = J3CreateOrderOutcome.UnknownOutcome,
            ErrorCode = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Unknown
        };
}

public enum J3CreateOrderOutcome
{
    Success = 0,
    DefiniteFailure = 1,
    UnknownOutcome = 2
}
