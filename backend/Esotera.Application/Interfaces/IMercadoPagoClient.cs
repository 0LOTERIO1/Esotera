namespace Esotera.Application.Interfaces;

public record MercadoPagoCreatePaymentCommand(
    decimal TransactionAmount,
    string Description,
    string ExternalReference,
    string PayerEmail,
    string? PayerCpf,
    string PaymentMethodId,
    string? Token,
    int Installments,
    string? IssuerId,
    string? NotificationUrl
);

public record MercadoPagoPaymentSnapshot(
    string Id,
    string Status,
    string StatusDetail,
    decimal TransactionAmount,
    string CurrencyId,
    string ExternalReference,
    string? PaymentMethodId,
    string? QrCode,
    string? QrCodeBase64,
    string? TicketUrl
);

public interface IMercadoPagoClient
{
    Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPaymentSnapshot> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default);
}
