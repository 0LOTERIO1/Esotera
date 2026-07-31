namespace Esotera.Application.Interfaces;

/// <summary>
/// Comando para criar order Pix na Orders API (fase 1 — somente Pix).
/// </summary>
public record MercadoPagoCreatePaymentCommand(
    decimal TransactionAmount,
    string? Description,
    string ExternalReference,
    string PayerEmail,
    string? PayerFirstName,
    string? PayerCpf,
    string PaymentMethodId,
    string? Token,
    int Installments,
    string? IssuerId,
    string? NotificationUrl,
    bool IsSandboxOfficialTest = false
);

/// <summary>
/// Snapshot seguro de uma order Mercado Pago (Orders API).
/// OrderId = ORD…; TransactionPaymentId = PAY… interno.
/// </summary>
public record MercadoPagoPaymentSnapshot(
    string OrderId,
    string? TransactionPaymentId,
    string Status,
    string StatusDetail,
    decimal TransactionAmount,
    string CurrencyId,
    string ExternalReference,
    string? PaymentMethodId,
    string? QrCode,
    string? QrCodeBase64,
    string? TicketUrl,
    string? DateOfExpiration
);

public interface IMercadoPagoClient
{
    /// <summary>Cria order Pix via POST /v1/orders.</summary>
    Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta order via GET /v1/orders/{orderId}.</summary>
    Task<MercadoPagoPaymentSnapshot> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);
}
