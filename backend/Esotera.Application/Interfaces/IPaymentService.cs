using Esotera.Application.DTOs.Payments;

namespace Esotera.Application.Interfaces;

public interface IPaymentService
{
    PaymentEnvironmentConfigDto GetPublicConfig();

    Task<CreatePaymentResponse> CreateForOrderAsync(
        Guid userId,
        Guid orderId,
        CreatePaymentRequest request,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pix oficial de teste R$ 50 em sandbox — não cria pedido comercial,
    /// não consome cupom/estoque e não aparece como venda.
    /// </summary>
    Task<SandboxPixTestResponse> CreateSandboxPixTestAsync(
        Guid userId,
        string paymentIdempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processa notificação do MP: valida assinatura (se configurada),
    /// consulta a order na API e atualiza o pedido de forma idempotente.
    /// Ignora orders de teste sandbox e IDs inexistentes.
    /// </summary>
    Task ProcessWebhookAsync(
        string? rawBody,
        string? xSignature,
        string? xRequestId,
        string? dataIdFromQuery,
        CancellationToken cancellationToken = default);
}
