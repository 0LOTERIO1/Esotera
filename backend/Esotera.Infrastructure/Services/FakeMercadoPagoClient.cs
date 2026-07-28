using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente fake para testes — sem credenciais e sem chamadas HTTP reais.
/// </summary>
public class FakeMercadoPagoClient : IMercadoPagoClient
{
    private readonly Dictionary<string, MercadoPagoPaymentSnapshot> _byId = new(StringComparer.Ordinal);
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

        var id = (++_seq).ToString();
        var isPix = string.Equals(command.PaymentMethodId, "pix", StringComparison.OrdinalIgnoreCase);
        var status = isPix ? "pending" : "approved";

        var snapshot = new MercadoPagoPaymentSnapshot(
            id,
            status,
            isPix ? "pending_waiting_transfer" : "accredited",
            command.TransactionAmount,
            "BRL",
            command.ExternalReference,
            command.PaymentMethodId,
            isPix ? "00020126fake-pix-copia-cola-test" : null,
            isPix ? "aW1hZ2UtZmFrZS1iYXNlNjQ=" : null,
            isPix ? "https://example.test/pix-ticket" : null);

        _byId[id] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<MercadoPagoPaymentSnapshot> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (_byId.TryGetValue(paymentId, out var snap))
            return Task.FromResult(snap);

        // Permite simular aprovação posterior em testes de webhook
        var pending = new MercadoPagoPaymentSnapshot(
            paymentId,
            "approved",
            "accredited",
            0m,
            "BRL",
            "",
            "pix",
            null,
            null,
            null);
        return Task.FromResult(pending);
    }

    public void Seed(MercadoPagoPaymentSnapshot snapshot) => _byId[snapshot.Id] = snapshot;

    public void SetStatus(string paymentId, string status, decimal amount, string externalReference)
    {
        _byId[paymentId] = new MercadoPagoPaymentSnapshot(
            paymentId,
            status,
            status,
            amount,
            "BRL",
            externalReference,
            "pix",
            "00020126fake-pix-copia-cola-test",
            "aW1hZ2UtZmFrZS1iYXNlNjQ=",
            null);
    }
}
