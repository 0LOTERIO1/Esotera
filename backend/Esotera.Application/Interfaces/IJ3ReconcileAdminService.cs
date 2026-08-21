using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

public interface IJ3ReconcileAdminService
{
    Task<J3ReconcileAdminOutcome> ReconcileAsync(
        Guid orderId,
        J3ReconcileConfirmRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record J3ReconcileConfirmRequest(
    string ConfirmOrderNumber,
    string ConfirmJ3OrderCode);

public sealed record J3ReconcileAdminResultDto(
    Guid OrderId,
    string? OrderNumber,
    Guid FulfillmentId,
    string FulfillmentStatus,
    string? FulfillmentLastErrorCode,
    string? J3OrderId,
    string? J3OrderCode,
    string? J3TrackingNumber,
    bool AlreadyReconciled,
    string Outcome,
    string? ErrorCode,
    bool LookupHttpSent,
    string OperationName);

public sealed class J3ReconcileAdminOutcome
{
    public int HttpStatus { get; init; }
    public string? ReasonCode { get; init; }
    public string? Message { get; init; }
    public J3ReconcileAdminResultDto? Body { get; init; }

    public static J3ReconcileAdminOutcome NotFound() =>
        new() { HttpStatus = 404 };

    public static J3ReconcileAdminOutcome BadRequest(string reasonCode, string message) =>
        new() { HttpStatus = 400, ReasonCode = reasonCode, Message = message };

    public static J3ReconcileAdminOutcome Conflict(
        string reasonCode,
        string message,
        J3ReconcileAdminResultDto? body = null) =>
        new() { HttpStatus = 409, ReasonCode = reasonCode, Message = message, Body = body };

    public static J3ReconcileAdminOutcome Ok(J3ReconcileAdminResultDto body) =>
        new() { HttpStatus = 200, Body = body };

    public static J3ReconcileAdminOutcome Unprocessable(
        string reasonCode,
        string message,
        J3ReconcileAdminResultDto body) =>
        new() { HttpStatus = 422, ReasonCode = reasonCode, Message = message, Body = body };
}
