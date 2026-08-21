using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Recovery Admin controlado: uma chamada a <see cref="IJ3ImportOrderByAccessKeyClient"/>.
/// Nunca createTmsOrders. Nunca promove J3Fulfillment para Created.
/// </summary>
public interface IJ3ImportOrderByAccessKeyAdminService
{
    Task<J3ImportByAccessKeyAdminOutcome> ImportAsync(
        Guid orderId,
        J3ImportByAccessKeyConfirmRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class J3ImportByAccessKeyAdminOutcome
{
    public required int HttpStatus { get; init; }
    public required string ReasonCode { get; init; }
    public required string Message { get; init; }
    public J3ImportByAccessKeyAdminResultDto? Body { get; init; }

    public static J3ImportByAccessKeyAdminOutcome NotFound() =>
        new()
        {
            HttpStatus = 404,
            ReasonCode = "OrderNotFound",
            Message = "Pedido não encontrado."
        };

    public static J3ImportByAccessKeyAdminOutcome BadRequest(string reasonCode, string message) =>
        new()
        {
            HttpStatus = 400,
            ReasonCode = reasonCode,
            Message = message
        };

    public static J3ImportByAccessKeyAdminOutcome Conflict(
        string reasonCode,
        string message,
        J3ImportByAccessKeyAdminResultDto? body = null) =>
        new()
        {
            HttpStatus = 409,
            ReasonCode = reasonCode,
            Message = message,
            Body = body
        };

    public static J3ImportByAccessKeyAdminOutcome Ok(J3ImportByAccessKeyAdminResultDto body) =>
        new()
        {
            HttpStatus = 200,
            ReasonCode = "Success",
            Message = "importOrderByAccessKey concluído (fulfillment não alterado).",
            Body = body
        };

    public static J3ImportByAccessKeyAdminOutcome Unprocessable(
        string reasonCode,
        string message,
        J3ImportByAccessKeyAdminResultDto body) =>
        new()
        {
            HttpStatus = 422,
            ReasonCode = reasonCode,
            Message = message,
            Body = body
        };
}
