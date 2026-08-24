namespace Esotera.Application.DTOs.Payments;

/// <summary>
/// Criação de pagamento via Orders API (Checkout Transparente).
/// Nunca inclui número de cartão, CVV ou validade — apenas token do Brick.
/// </summary>
public record CreatePaymentRequest(
    string? Token,
    string PaymentMethodId,
    int? Installments,
    string? IssuerId,
    string? PayerEmail,
    /// <summary>
    /// bank_transfer | credit_card | debit_card | ticket.
    /// Null + paymentMethodId=pix → bank_transfer (compatível com front Pix-only).
    /// </summary>
    string? PaymentMethodType = null,
    string? PayerIdentificationType = null,
    string? PayerIdentificationNumber = null
);

public record CreatePaymentResponse(
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    string Status,
    string? MercadoPagoOrderId,
    string? MercadoPagoPaymentId,
    string? TicketUrl,
    string? QrCode,
    string? QrCodeBase64,
    string? DateOfExpiration,
    string Message,
    string? DigitableLine = null,
    string? BarcodeContent = null
);

/// <summary>Config pública do MP (sem secrets).</summary>
public record PaymentEnvironmentConfigDto(
    string Environment,
    bool SandboxPixEnabled,
    decimal SandboxPixAmount,
    bool CommercialCheckoutAllowedInTest
);

/// <summary>Resultado do Pix de teste isolado (não é pedido comercial).</summary>
public record SandboxPixTestResponse(
    string MercadoPagoOrderId,
    string? MercadoPagoPaymentId,
    decimal Amount,
    string Currency,
    string Status,
    string StatusDetail,
    string ExternalReference,
    string? TicketUrl,
    string? QrCode,
    string? QrCodeBase64,
    string? DateOfExpiration,
    string Message,
    bool IsSandboxTest
);

public record MercadoPagoWebhookRequest(
    string? Action,
    string? Type,
    string? Topic,
    MercadoPagoWebhookData? Data
);

public record MercadoPagoWebhookData(string? Id);
