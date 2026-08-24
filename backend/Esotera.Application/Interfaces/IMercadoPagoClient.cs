namespace Esotera.Application.Interfaces;

/// <summary>
/// Comando para criar order na Orders API (Checkout Transparente).
/// </summary>
public record MercadoPagoCreatePaymentCommand(
    decimal TransactionAmount,
    string? Description,
    string ExternalReference,
    string PayerEmail,
    string? PayerFirstName,
    string? PayerLastName,
    string? PayerCpf,
    /// <summary>ID recebido do Brick (ex.: pix, visa, bolbradesco).</summary>
    string PaymentMethodId,
    /// <summary>bank_transfer | credit_card | debit_card | ticket</summary>
    string PaymentMethodType,
    string? Token,
    int? Installments,
    string? IssuerId,
    string? NotificationUrl,
    bool IsSandboxOfficialTest = false,
    string? PayerZipCode = null,
    string? PayerStreetName = null,
    string? PayerStreetNumber = null,
    string? PayerNeighborhood = null,
    string? PayerCity = null,
    string? PayerState = null,
    string? PayerComplement = null
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
    string? DateOfExpiration,
    string? DigitableLine = null,
    string? BarcodeContent = null
);

public interface IMercadoPagoClient
{
    /// <summary>Cria order via POST /v1/orders.</summary>
    Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta order via GET /v1/orders/{orderId}.</summary>
    Task<MercadoPagoPaymentSnapshot> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);
}
