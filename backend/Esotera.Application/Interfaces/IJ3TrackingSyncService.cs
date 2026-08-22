namespace Esotera.Application.Interfaces;

/// <summary>
/// Sync manual admin: searchOrderByCode (read-only) → persiste J3RemoteStatus.
/// Zero createTmsOrders / importOrderByAccessKey / processor.
/// </summary>
public interface IJ3TrackingSyncService
{
    Task<J3TrackingSyncOutcome> SyncAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed record J3TrackingSyncResultDto(
    Guid OrderId,
    string? OrderNumber,
    Guid FulfillmentId,
    string FulfillmentStatus,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    string? J3RemoteStatus,
    DateTime? J3LastStatusSyncAtUtc,
    string? J3LastStatusSyncErrorCode,
    DateTime? J3LastStatusSyncErrorAtUtc,
    string Outcome,
    string? ErrorCode,
    bool LookupHttpSent,
    string OperationName);

public sealed class J3TrackingSyncOutcome
{
    public int HttpStatus { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }
    public J3TrackingSyncResultDto? Body { get; init; }

    public static J3TrackingSyncOutcome NotFound() =>
        new() { HttpStatus = 404 };

    public static J3TrackingSyncOutcome Conflict(
        string reasonCode,
        string message,
        J3TrackingSyncResultDto? body = null) =>
        new() { HttpStatus = 409, ReasonCode = reasonCode, Message = message, Body = body };

    public static J3TrackingSyncOutcome Unprocessable(
        string reasonCode,
        string message,
        J3TrackingSyncResultDto body) =>
        new() { HttpStatus = 422, ReasonCode = reasonCode, Message = message, Body = body };

    public static J3TrackingSyncOutcome Ok(J3TrackingSyncResultDto body) =>
        new() { HttpStatus = 200, Body = body };
}
