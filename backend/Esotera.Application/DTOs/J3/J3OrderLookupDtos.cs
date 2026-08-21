namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Resposta tipada de searchOrderByCode (schema J3 confirmado).
/// Sem campo code — o código confirmado vem do request + trackingNumber.
/// </summary>
public sealed record J3SearchOrderByCodeResponseDto(
    string Id,
    string? Date,
    string? Nf,
    string Status,
    string? StoreName,
    string Ecommerce,
    IReadOnlyList<J3SearchOrderByCodeDeliveryPointDto> DeliveryPoints);

public sealed record J3SearchOrderByCodeDeliveryPointDto(
    string AddressName,
    string AddressZipCode,
    string? TrackingNumber);

/// <summary>
/// Snapshot canônico pós-match para persistir no J3Fulfillment.
/// OrderCode = confirmJ3OrderCode (após tracking confirmar).
/// DeliveryPointId / StampUrl nunca inventados (sempre null neste lookup).
/// </summary>
public sealed record J3RemoteOrderSnapshot(
    string OrderId,
    string OrderCode,
    string TrackingNumber,
    string? Status,
    string? StoreName,
    string? Ecommerce,
    string? Date,
    string? Nf,
    string DeliveryCepDigits);

public sealed class J3OrderLookupResult
{
    public required J3OrderLookupOutcome Outcome { get; init; }
    public J3SearchOrderByCodeResponseDto? Response { get; init; }
    public J3RemoteOrderSnapshot? Snapshot { get; init; }
    public string? ErrorCode { get; init; }

    public static J3OrderLookupResult Found(
        J3SearchOrderByCodeResponseDto response,
        J3RemoteOrderSnapshot snapshot) =>
        new()
        {
            Outcome = J3OrderLookupOutcome.Found,
            Response = response,
            Snapshot = snapshot
        };

    public static J3OrderLookupResult NotFound() =>
        new() { Outcome = J3OrderLookupOutcome.NotFound, ErrorCode = J3ReconcileErrorCodes.NotFound };

    public static J3OrderLookupResult Failed(string errorCode) =>
        new()
        {
            Outcome = J3OrderLookupOutcome.Failed,
            ErrorCode = errorCode
        };
}

public enum J3OrderLookupOutcome
{
    Found = 0,
    NotFound = 1,
    Failed = 2
}

public static class J3ReconcileErrorCodes
{
    public const string NotFound = "RECONCILE_NOT_FOUND";
    public const string Multiple = "RECONCILE_MULTIPLE";
    public const string CodeMismatch = "RECONCILE_CODE_MISMATCH";
    public const string CepMismatch = "RECONCILE_CEP_MISMATCH";
    public const string TrackingMismatch = "RECONCILE_TRACKING_MISMATCH";
    public const string DeliveryPointMissing = "RECONCILE_DELIVERY_POINT_MISSING";
    public const string NotEligible = "RECONCILE_NOT_ELIGIBLE";
    public const string ConfirmMismatch = "RECONCILE_CONFIRM_MISMATCH";
    public const string LookupFailed = "RECONCILE_LOOKUP_FAILED";
    public const string MissingOrderId = "RECONCILE_MISSING_ORDER_ID";
}
