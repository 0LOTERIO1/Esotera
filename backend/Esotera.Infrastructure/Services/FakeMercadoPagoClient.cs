using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente fake para testes — Orders API (Pix), sem credenciais e sem HTTP real.
/// </summary>
public class FakeMercadoPagoClient : IMercadoPagoClient
{
    private readonly Dictionary<string, MercadoPagoPaymentSnapshot> _byOrderId =
        new(StringComparer.Ordinal);
    private int _seq;

    public string LastIdempotencyKey { get; private set; } = "";
    public List<MercadoPagoCreatePaymentCommand> Created { get; } = new();

    public Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        LastIdempotencyKey = idempotencyKey;
        Created.Add(command);

        var method = (command.PaymentMethodId ?? "").Trim().ToLowerInvariant();
        if (method is not "pix")
            throw new InvalidOperationException("Fake MP: somente Pix nesta fase.");

        var n = ++_seq;
        var orderId = $"ORDFAKE{n:D20}";
        var payId = $"PAYFAKE{n:D20}";

        var snapshot = new MercadoPagoPaymentSnapshot(
            orderId,
            payId,
            "action_required",
            "waiting_transfer",
            command.TransactionAmount,
            "BRL",
            command.ExternalReference,
            "pix",
            "00020126fake-pix-copia-cola-test",
            "aW1hZ2UtZmFrZS1iYXNlNjQ=",
            "https://example.test/pix-ticket",
            DateTime.UtcNow.AddHours(24).ToString("O"));

        _byOrderId[orderId] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<MercadoPagoPaymentSnapshot> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (_byOrderId.TryGetValue(orderId, out var snap))
            return Task.FromResult(snap);

        var pending = new MercadoPagoPaymentSnapshot(
            orderId,
            null,
            "processed",
            "accredited",
            0m,
            "BRL",
            "",
            "pix",
            null,
            null,
            null,
            null);
        return Task.FromResult(pending);
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
            "pix",
            existing?.QrCode ?? "00020126fake-pix-copia-cola-test",
            existing?.QrCodeBase64 ?? "aW1hZ2UtZmFrZS1iYXNlNjQ=",
            existing?.TicketUrl,
            existing?.DateOfExpiration);
    }
}
