using Esotera.Application.Interfaces;
using Esotera.Application.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente fake para testes — Orders API multi-meios, sem HTTP real.
/// </summary>
public class FakeMercadoPagoClient : IMercadoPagoClient
{
    private readonly Dictionary<string, MercadoPagoPaymentSnapshot> _byOrderId =
        new(StringComparer.Ordinal);
    private int _seq;

    public string LastIdempotencyKey { get; private set; } = "";
    public List<MercadoPagoCreatePaymentCommand> Created { get; } = new();

    /// <summary>Quando true, CreatePaymentAsync lança ValidationException simulando invalid_email_for_sandbox.</summary>
    public bool FailNextCreateWithSandboxEmailError { get; set; }

    /// <summary>Próximo CreatePaymentAsync lança esta exception (timeout/rede ambígua).</summary>
    public Exception? FailNextCreateWithException { get; set; }

    /// <summary>Próximo GetOrderAsync lança esta exception.</summary>
    public Exception? FailNextGetWithException { get; set; }

    /// <summary>Status/status_detail forçados na próxima criação (ex.: rejected).</summary>
    public string? NextCreateStatus { get; set; }
    public string? NextCreateStatusDetail { get; set; }

    public Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        LastIdempotencyKey = idempotencyKey;

        if (FailNextCreateWithException is not null)
        {
            var ex = FailNextCreateWithException;
            FailNextCreateWithException = null;
            throw ex;
        }

        Created.Add(command);

        var methodType = (command.PaymentMethodType ?? "").Trim().ToLowerInvariant();
        var methodId = (command.PaymentMethodId ?? "").Trim().ToLowerInvariant();

        if (methodType is not ("bank_transfer" or "credit_card" or "debit_card" or "ticket"))
            throw new InvalidOperationException($"Fake MP: tipo inválido ({methodType}).");

        if (methodType is ("credit_card" or "debit_card") && string.IsNullOrWhiteSpace(command.Token))
            throw new Application.Exceptions.ValidationException("token", "Token do cartão é obrigatório.");

        if (methodType == "ticket" && string.IsNullOrWhiteSpace(command.PayerCpf))
            throw new Application.Exceptions.ValidationException(
                "payerIdentification",
                "CPF do pagador é obrigatório para boleto.");

        if (FailNextCreateWithSandboxEmailError)
        {
            FailNextCreateWithSandboxEmailError = false;
            throw new Application.Exceptions.ValidationException(
                "payment",
                MercadoPagoOptions.CommercialSandboxBlockedMessage);
        }

        var n = ++_seq;
        var orderId = $"ORDFAKE{n:D20}";
        var payId = $"PAYFAKE{n:D20}";

        var status = NextCreateStatus ?? DefaultStatus(methodType);
        var detail = NextCreateStatusDetail ?? DefaultDetail(methodType);
        NextCreateStatus = null;
        NextCreateStatusDetail = null;

        var ordersMethodId = methodType == "ticket"
            ? MercadoPagoHttpClient.MapTicketPaymentMethodId(methodId)
            : methodType == "bank_transfer" ? "pix" : methodId;

        string? qr = null;
        string? qrB64 = null;
        string? ticketUrl = null;
        string? digitable = null;
        string? barcode = null;

        if (methodType == "bank_transfer")
        {
            qr = "00020126fake-pix-copia-cola-test";
            qrB64 = "aW1hZ2UtZmFrZS1iYXNlNjQ=";
            ticketUrl = "https://example.test/pix-ticket";
        }
        else if (methodType == "ticket")
        {
            ticketUrl = "https://example.test/boleto-ticket";
            digitable = "23793.38128 60000.000003 00000.000400 1 84340000010000";
            barcode = "23791843400000100003381260000000000000000000";
        }

        var snapshot = new MercadoPagoPaymentSnapshot(
            orderId,
            payId,
            status,
            detail,
            command.TransactionAmount,
            "BRL",
            command.ExternalReference,
            ordersMethodId,
            qr,
            qrB64,
            ticketUrl,
            DateTime.UtcNow.AddDays(methodType == "ticket" ? 3 : 0).AddHours(methodType == "bank_transfer" ? 24 : 0)
                .ToString("O"),
            digitable,
            barcode);

        _byOrderId[orderId] = snapshot;
        return Task.FromResult(snapshot);
    }

    private static string DefaultStatus(string methodType) =>
        methodType is "credit_card" or "debit_card" ? "processed" : "action_required";

    private static string DefaultDetail(string methodType) =>
        methodType switch
        {
            "credit_card" or "debit_card" => "accredited",
            "ticket" => "pending_waiting_payment",
            _ => "waiting_transfer",
        };

    public Task<MercadoPagoPaymentSnapshot> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (FailNextGetWithException is not null)
        {
            var ex = FailNextGetWithException;
            FailNextGetWithException = null;
            throw ex;
        }

        if (_byOrderId.TryGetValue(orderId, out var snap))
            return Task.FromResult(snap);

        throw new Application.Exceptions.NotFoundException("Order Mercado Pago", orderId);
    }

    public void Seed(MercadoPagoPaymentSnapshot snapshot) =>
        _byOrderId[snapshot.OrderId] = snapshot;

    public void SetStatus(
        string orderId,
        string status,
        decimal amount,
        string externalReference,
        string? paymentId = null,
        string? statusDetail = null)
    {
        _byOrderId.TryGetValue(orderId, out var existing);
        _byOrderId[orderId] = new MercadoPagoPaymentSnapshot(
            orderId,
            paymentId ?? existing?.TransactionPaymentId,
            status,
            statusDetail ?? status,
            amount,
            "BRL",
            externalReference,
            existing?.PaymentMethodId ?? "pix",
            existing?.QrCode ?? "00020126fake-pix-copia-cola-test",
            existing?.QrCodeBase64 ?? "aW1hZ2UtZmFrZS1iYXNlNjQ=",
            existing?.TicketUrl,
            existing?.DateOfExpiration,
            existing?.DigitableLine,
            existing?.BarcodeContent);
    }
}
