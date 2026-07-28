using Esotera.Application.DTOs.Payments;

namespace Esotera.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreateForOrderAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processa notificação do MP: valida assinatura (se configurada),
    /// consulta o pagamento na API e atualiza o pedido de forma idempotente.
    /// </summary>
    Task ProcessWebhookAsync(
        string? rawBody,
        string? xSignature,
        string? xRequestId,
        string? dataIdFromQuery,
        CancellationToken cancellationToken = default);
}
