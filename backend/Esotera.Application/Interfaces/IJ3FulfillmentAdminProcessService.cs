using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Orquestração Admin: eligibility → EnsurePending → Processor.
/// Zero HTTP se inelegível / flag off. Sem body do cliente.
/// </summary>
public interface IJ3FulfillmentAdminProcessService
{
    Task<J3FulfillmentAdminProcessOutcome> ProcessOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado tipado para o controller mapear HTTP.</summary>
public sealed class J3FulfillmentAdminProcessOutcome
{
    public required int HttpStatus { get; init; }
    public required string ReasonCode { get; init; }
    public required string Message { get; init; }
    public J3FulfillmentAdminProcessDto? Body { get; init; }

    public bool IsSuccess => HttpStatus is >= 200 and < 300;

    public static J3FulfillmentAdminProcessOutcome NotFound() =>
        new()
        {
            HttpStatus = 404,
            ReasonCode = "OrderNotFound",
            Message = "Pedido não encontrado."
        };

    public static J3FulfillmentAdminProcessOutcome Conflict(
        string reasonCode,
        string message,
        J3FulfillmentAdminProcessDto? body = null) =>
        new()
        {
            HttpStatus = 409,
            ReasonCode = reasonCode,
            Message = message,
            Body = body
        };

    public static J3FulfillmentAdminProcessOutcome Ok(J3FulfillmentAdminProcessDto body) =>
        new()
        {
            HttpStatus = 200,
            ReasonCode = body.EligibilityReason,
            Message = body.Processed
                ? "Processamento J3 concluído."
                : "Estado J3 atual.",
            Body = body
        };
}
