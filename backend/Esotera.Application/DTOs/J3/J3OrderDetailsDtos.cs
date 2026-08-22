using Esotera.Application.Shipping;

namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Resposta tipada de getOrderDetails (schema J3 confirmado por introspecção).
/// Status é capturado mas NÃO persistido pela hidratação de identificadores.
/// </summary>
public sealed record J3OrderDetailsDto(
    string Id,
    string Status,
    J3DeliveryPointDetailsDto? DeliveryPoint);

public sealed record J3DeliveryPointDetailsDto(
    string Id,
    string TrackingNumber,
    string AddressZipCode,
    string AddressName);

public sealed class J3OrderDetailsLookupResult
{
    public required J3OrderDetailsLookupOutcome Outcome { get; init; }
    public J3OrderDetailsDto? Response { get; init; }
    public string? ErrorCode { get; init; }

    public static J3OrderDetailsLookupResult Found(J3OrderDetailsDto response) =>
        new()
        {
            Outcome = J3OrderDetailsLookupOutcome.Found,
            Response = response
        };

    public static J3OrderDetailsLookupResult NotFound() =>
        new()
        {
            Outcome = J3OrderDetailsLookupOutcome.NotFound,
            ErrorCode = J3IdentifierHydrationErrorCodes.NotFound
        };

    public static J3OrderDetailsLookupResult Failed(string errorCode) =>
        new()
        {
            Outcome = J3OrderDetailsLookupOutcome.Failed,
            ErrorCode = errorCode
        };
}

public enum J3OrderDetailsLookupOutcome
{
    Found = 0,
    NotFound = 1,
    Failed = 2
}
