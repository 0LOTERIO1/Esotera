namespace Esotera.Application.Interfaces;

/// <summary>
/// Hidratação manual admin: getOrderDetails (read-only) → J3OrderCode + J3TrackingNumber.
/// Zero createTmsOrders / importOrderByAccessKey / processor / tracking sync.
/// Não altera J3Fulfillment.Status de integração.
/// </summary>
public interface IJ3IdentifierHydrationService
{
    Task<J3IdentifierHydrationOutcome> HydrateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed record J3IdentifierHydrationResultDto(
    Guid OrderId,
    string? OrderNumber,
    Guid FulfillmentId,
    string FulfillmentStatus,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    string Outcome,
    string? ErrorCode,
    bool LookupHttpSent,
    string OperationName);

public sealed class J3IdentifierHydrationOutcome
{
    public int HttpStatus { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }
    public J3IdentifierHydrationResultDto? Body { get; init; }

    public static J3IdentifierHydrationOutcome NotFound() =>
        new() { HttpStatus = 404 };

    public static J3IdentifierHydrationOutcome Conflict(
        string reasonCode,
        string message,
        J3IdentifierHydrationResultDto? body = null) =>
        new() { HttpStatus = 409, ReasonCode = reasonCode, Message = message, Body = body };

    public static J3IdentifierHydrationOutcome Unprocessable(
        string reasonCode,
        string message,
        J3IdentifierHydrationResultDto body) =>
        new() { HttpStatus = 422, ReasonCode = reasonCode, Message = message, Body = body };

    public static J3IdentifierHydrationOutcome Ok(J3IdentifierHydrationResultDto body) =>
        new() { HttpStatus = 200, Body = body };
}
