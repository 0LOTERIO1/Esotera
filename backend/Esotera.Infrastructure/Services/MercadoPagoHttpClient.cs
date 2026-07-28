using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public class MercadoPagoHttpClient : IMercadoPagoClient
{
    private readonly HttpClient _http;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoHttpClient> _logger;

    public MercadoPagoHttpClient(
        HttpClient http,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MercadoPagoPaymentSnapshot> CreatePaymentAsync(
        MercadoPagoCreatePaymentCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = command.TransactionAmount,
            ["description"] = command.Description,
            ["external_reference"] = command.ExternalReference,
            ["payment_method_id"] = command.PaymentMethodId,
            ["installments"] = command.Installments,
            ["payer"] = BuildPayer(command.PayerEmail, command.PayerCpf),
        };

        if (!string.IsNullOrWhiteSpace(command.Token))
            body["token"] = command.Token;
        if (!string.IsNullOrWhiteSpace(command.IssuerId))
            body["issuer_id"] = command.IssuerId;
        if (!string.IsNullOrWhiteSpace(command.NotificationUrl))
            body["notification_url"] = command.NotificationUrl;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Mercado Pago CreatePayment falhou com status {Status} (sem logar body sensível).",
                (int)response.StatusCode);
            throw new ValidationException(
                "payment",
                "Não foi possível criar o pagamento. Verifique os dados e tente novamente.");
        }

        return ParsePayment(raw);
    }

    public async Task<MercadoPagoPaymentSnapshot> GetPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{paymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Mercado Pago GetPayment falhou com status {Status} para id informado.",
                (int)response.StatusCode);
            throw new NotFoundException("Pagamento Mercado Pago", paymentId);
        }

        return ParsePayment(raw);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new ValidationException(
                "payment",
                "Pagamento ainda não está configurado no servidor.");
    }

    private static object BuildPayer(string email, string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return new { email };

        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        return new
        {
            email,
            identification = new { type = "CPF", number = digits }
        };
    }

    private static MercadoPagoPaymentSnapshot ParsePayment(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var id = root.GetProperty("id").ToString();
        var status = root.GetProperty("status").GetString() ?? "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd)
            ? sd.GetString() ?? ""
            : "";
        var amount = root.TryGetProperty("transaction_amount", out var amt)
            ? amt.GetDecimal()
            : 0m;
        var currency = root.TryGetProperty("currency_id", out var cur)
            ? cur.GetString() ?? "BRL"
            : "BRL";
        var externalRef = root.TryGetProperty("external_reference", out var er)
            ? er.GetString() ?? ""
            : "";
        var methodId = root.TryGetProperty("payment_method_id", out var pm)
            ? pm.GetString()
            : null;

        string? qrCode = null;
        string? qrBase64 = null;
        string? ticketUrl = null;
        if (root.TryGetProperty("point_of_interaction", out var poi)
            && poi.TryGetProperty("transaction_data", out var td))
        {
            if (td.TryGetProperty("qr_code", out var qr))
                qrCode = qr.GetString();
            if (td.TryGetProperty("qr_code_base64", out var qrb))
                qrBase64 = qrb.GetString();
            if (td.TryGetProperty("ticket_url", out var tu))
                ticketUrl = tu.GetString();
        }

        return new MercadoPagoPaymentSnapshot(
            id,
            status,
            statusDetail,
            amount,
            currency,
            externalRef,
            methodId,
            qrCode,
            qrBase64,
            ticketUrl);
    }
}
