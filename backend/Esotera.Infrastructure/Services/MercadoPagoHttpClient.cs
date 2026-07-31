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

/// <summary>
/// Cliente HTTP da Orders API (Checkout Transparente). Fase 1: somente Pix.
/// </summary>
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

        var methodId = (command.PaymentMethodId ?? "").Trim().ToLowerInvariant();
        if (methodId is not "pix")
        {
            throw new ValidationException(
                "paymentMethodId",
                "Nesta fase somente Pix está disponível. Cartão e boleto em breve.");
        }

        var amount = FormatAmount(command.TransactionAmount);
        var body = new Dictionary<string, object?>
        {
            ["type"] = "online",
            ["external_reference"] = command.ExternalReference,
            ["description"] = command.Description,
            ["total_amount"] = amount,
            ["payer"] = BuildPayer(command.PayerEmail, command.PayerCpf),
            ["transactions"] = new Dictionary<string, object?>
            {
                ["payments"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["amount"] = amount,
                        ["payment_method"] = new Dictionary<string, object?>
                        {
                            ["id"] = "pix",
                            ["type"] = "bank_transfer",
                        },
                    },
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/orders")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        ApplyAuth(request, idempotencyKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogSafeApiError("CreateOrder", response, raw);
            throw new ValidationException(
                "payment",
                "Não foi possível criar o pagamento Pix. Verifique os dados e tente novamente.");
        }

        var snapshot = ParseOrder(raw);
        _logger.LogInformation(
            "Mercado Pago order criada: OrderId={OrderId} PaymentId={PaymentId} Status={Status} StatusDetail={StatusDetail}",
            snapshot.OrderId,
            snapshot.TransactionPaymentId ?? "(ausente)",
            snapshot.Status,
            snapshot.StatusDetail);
        return snapshot;
    }

    public async Task<MercadoPagoPaymentSnapshot> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(orderId))
            throw new ValidationException("orderId", "ID da order Mercado Pago inválido.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/orders/{orderId.Trim()}");
        ApplyAuth(request, idempotencyKey: null);

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogSafeApiError("GetOrder", response, raw);
            throw new NotFoundException("Order Mercado Pago", orderId);
        }

        return ParseOrder(raw);
    }

    private void ApplyAuth(HttpRequestMessage request, string? idempotencyKey)
    {
        var accessToken = (_options.AccessToken ?? string.Empty).Trim();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new ValidationException(
                "payment",
                "Pagamento ainda não está configurado no servidor.");
    }

    /// <summary>
    /// Log seguro de erro da API MP — sem Access Token, body da request, CPF, e-mail ou payload bruto.
    /// </summary>
    private void LogSafeApiError(string operation, HttpResponseMessage response, string rawBody)
    {
        var httpStatus = (int)response.StatusCode;
        var wwwAuthenticate = response.Headers.WwwAuthenticate.Count > 0
            ? string.Join("; ", response.Headers.WwwAuthenticate.Select(v => v.ToString()))
            : "(ausente)";
        var requestId = "(ausente)";
        if (response.Headers.TryGetValues("x-request-id", out var reqIds))
            requestId = reqIds.FirstOrDefault() ?? "(ausente)";

        string error = "(ausente)";
        string message = "(ausente)";
        string responseStatus = "(ausente)";
        string causes = "(ausente)";

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning(
                "MercadoPago erro seguro ({Operation}):\nHttpStatus={HttpStatus}\nError={Error}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}\nJsonParse=corpo vazio",
                operation,
                httpStatus,
                error,
                message,
                responseStatus,
                causes,
                wwwAuthenticate,
                requestId);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorEl))
                error = ReadStringish(errorEl);
            if (root.TryGetProperty("message", out var messageEl))
                message = ReadStringish(messageEl);
            if (root.TryGetProperty("status", out var statusEl))
                responseStatus = ReadStringish(statusEl);

            if (root.TryGetProperty("cause", out var causeEl)
                && causeEl.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in causeEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    string? code = null;
                    string? description = null;
                    if (item.TryGetProperty("code", out var codeEl))
                        code = ReadStringish(codeEl);
                    if (item.TryGetProperty("description", out var descEl))
                        description = descEl.GetString();
                    if (description is null && item.TryGetProperty("message", out var causeMsg))
                        description = causeMsg.GetString();
                    if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(description))
                        parts.Add($"{code ?? "?"}:{description ?? "?"}");
                }

                causes = parts.Count > 0 ? string.Join(" | ", parts) : "(vazio)";
            }

            _logger.LogWarning(
                "MercadoPago erro seguro ({Operation}):\nHttpStatus={HttpStatus}\nError={Error}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}",
                operation,
                httpStatus,
                error,
                message,
                responseStatus,
                causes,
                wwwAuthenticate,
                requestId);
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "MercadoPago erro seguro ({Operation}):\nHttpStatus={HttpStatus}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}\nJsonParse=nao foi possivel interpretar a resposta (conteudo bruto nao registrado)",
                operation,
                httpStatus,
                wwwAuthenticate,
                requestId);
        }
    }

    private static string ReadStringish(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "(ausente)",
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => el.ToString()
        };

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

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

    private static MercadoPagoPaymentSnapshot ParseOrder(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var orderId = root.GetProperty("id").ToString();
        var status = root.TryGetProperty("status", out var st)
            ? st.GetString() ?? "unknown"
            : "unknown";
        var statusDetail = root.TryGetProperty("status_detail", out var sd)
            ? sd.GetString() ?? ""
            : "";
        var externalRef = root.TryGetProperty("external_reference", out var er)
            ? er.GetString() ?? ""
            : "";

        var amount = 0m;
        if (root.TryGetProperty("total_amount", out var totalEl))
            amount = ParseDecimal(totalEl);
        else if (root.TryGetProperty("total_paid_amount", out var paidEl))
            amount = ParseDecimal(paidEl);

        var currency = "BRL";
        if (root.TryGetProperty("currency_id", out var cur) && cur.ValueKind == JsonValueKind.String)
            currency = cur.GetString() ?? "BRL";

        string? payId = null;
        string? methodId = null;
        string? qrCode = null;
        string? qrBase64 = null;
        string? ticketUrl = null;
        string? dateOfExpiration = null;

        if (root.TryGetProperty("transactions", out var tx)
            && tx.TryGetProperty("payments", out var payments)
            && payments.ValueKind == JsonValueKind.Array)
        {
            foreach (var payment in payments.EnumerateArray())
            {
                if (payment.TryGetProperty("id", out var pid))
                    payId = pid.ToString();

                if (payment.TryGetProperty("amount", out var payAmt) && amount <= 0)
                    amount = ParseDecimal(payAmt);

                if (payment.TryGetProperty("status", out var pst)
                    && string.Equals(status, "unknown", StringComparison.OrdinalIgnoreCase))
                    status = pst.GetString() ?? status;

                if (payment.TryGetProperty("status_detail", out var psd)
                    && string.IsNullOrWhiteSpace(statusDetail))
                    statusDetail = psd.GetString() ?? "";

                if (payment.TryGetProperty("date_of_expiration", out var exp))
                    dateOfExpiration = ReadStringish(exp);

                if (payment.TryGetProperty("expiration_time", out var expTime)
                    && string.IsNullOrWhiteSpace(dateOfExpiration))
                    dateOfExpiration = ReadStringish(expTime);

                if (payment.TryGetProperty("payment_method", out var pm)
                    && pm.ValueKind == JsonValueKind.Object)
                {
                    if (pm.TryGetProperty("id", out var pmid))
                        methodId = pmid.GetString();
                    if (pm.TryGetProperty("qr_code", out var qr))
                        qrCode = qr.GetString();
                    if (pm.TryGetProperty("qr_code_base64", out var qrb))
                        qrBase64 = qrb.GetString();
                    if (pm.TryGetProperty("ticket_url", out var tu))
                        ticketUrl = tu.GetString();
                }

                // Usa o primeiro payment da order.
                break;
            }
        }

        return new MercadoPagoPaymentSnapshot(
            orderId,
            payId,
            status,
            statusDetail,
            amount,
            currency,
            externalRef,
            methodId,
            qrCode,
            qrBase64,
            ticketUrl,
            dateOfExpiration);
    }

    private static decimal ParseDecimal(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            return d;
        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s))
            return s;
        return 0m;
    }
}
