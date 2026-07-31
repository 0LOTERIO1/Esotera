namespace Esotera.Application.DTOs.Payments;

/// <summary>
/// Criação de pagamento. Fase 1: somente Pix (Orders API).
/// Nunca inclui número de cartão ou CVV.
/// </summary>
public record CreatePaymentRequest(
    string? Token,
    string PaymentMethodId,
    int? Installments,
    string? IssuerId,
    string? PayerEmail
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
    string Message
);

public record MercadoPagoWebhookRequest(
    string? Action,
    string? Type,
    string? Topic,
    MercadoPagoWebhookData? Data
);

public record MercadoPagoWebhookData(string? Id);
