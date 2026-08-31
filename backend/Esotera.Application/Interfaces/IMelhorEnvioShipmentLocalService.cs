namespace Esotera.Application.Interfaces;

/// <summary>
/// Registro LOCAL do ciclo logístico Melhor Envio (Fase B).
/// ZERO chamadas à API do Melhor Envio: não insere no carrinho, não compra, não gera etiqueta.
/// </summary>
public interface IMelhorEnvioShipmentLocalService
{
    /// <summary>
    /// Garante o registro 1:1 se o pedido está payment_approved e o frete é Melhor Envio.
    /// Status inicial: ready_to_create quando já existe NF-e autorizada, senão waiting_invoice.
    /// Idempotente via unique OrderId. Independente de qualquer feature flag — a obrigação
    /// local existe sempre que o cliente pagou. Zero HTTP.
    /// </summary>
    Task EnsureAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promove waiting_invoice → ready_to_create quando o pedido passa a ter NF-e autorizada.
    /// Não rebaixa status já avançado nem reabre failed/cancelled. Zero HTTP.
    /// </summary>
    Task SyncInvoiceReadinessAsync(Guid orderId, CancellationToken cancellationToken = default);
}
