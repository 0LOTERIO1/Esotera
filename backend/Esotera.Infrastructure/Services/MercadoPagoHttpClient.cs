using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        var accessToken = (_options.AccessToken ?? string.Empty).Trim();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        LogSafeAccessTokenDiagnostic(
            accessToken,
            new Uri(_http.BaseAddress!, "/v1/payments").AbsoluteUri,
            request.Headers.Authorization?.Scheme);

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogSafeCreatePaymentError(response, raw);
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
        var accessToken = (_options.AccessToken ?? string.Empty).Trim();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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

    /// <summary>
    /// Diagnóstico temporário do erro CreatePayment — só campos seguros do MP.
    /// Nunca registra Access Token, body da requisição, CPF, e-mail, QR ou payload bruto.
    /// </summary>
    private void LogSafeCreatePaymentError(HttpResponseMessage response, string rawBody)
    {
        var httpStatus = (int)response.StatusCode;
        var wwwAuthenticate = response.Headers.WwwAuthenticate.Count > 0
            ? string.Join("; ", response.Headers.WwwAuthenticate.Select(v => v.ToString()))
            : "(ausente)";
        var requestId = "(ausente)";
        if (response.Headers.TryGetValues("x-request-id", out var reqIds))
            requestId = reqIds.FirstOrDefault() ?? "(ausente)";
        else if (response.Headers.TryGetValues("X-Request-Id", out var reqIds2))
            requestId = reqIds2.FirstOrDefault() ?? "(ausente)";

        string error = "(ausente)";
        string message = "(ausente)";
        string responseStatus = "(ausente)";
        string causes = "(ausente)";

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning(
                "MercadoPago erro seguro:\nHttpStatus={HttpStatus}\nError={Error}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}\nJsonParse=corpo vazio",
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
                error = errorEl.ValueKind == JsonValueKind.String
                    ? (errorEl.GetString() ?? "(ausente)")
                    : errorEl.ToString();

            if (root.TryGetProperty("message", out var messageEl))
                message = messageEl.ValueKind == JsonValueKind.String
                    ? (messageEl.GetString() ?? "(ausente)")
                    : messageEl.ToString();

            if (root.TryGetProperty("status", out var statusEl))
                responseStatus = statusEl.ValueKind switch
                {
                    JsonValueKind.Number => statusEl.GetRawText(),
                    JsonValueKind.String => statusEl.GetString() ?? "(ausente)",
                    _ => statusEl.ToString()
                };

            if (root.TryGetProperty("cause", out var causeEl)
                && causeEl.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in causeEl.EnumerateArray())
                {
                    string? code = null;
                    string? description = null;
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.TryGetProperty("code", out var codeEl))
                            code = codeEl.ValueKind == JsonValueKind.Number
                                ? codeEl.GetRawText()
                                : codeEl.GetString();
                        if (item.TryGetProperty("description", out var descEl))
                            description = descEl.GetString();
                        // Alguns payloads usam "message" dentro de cause.
                        if (description is null && item.TryGetProperty("message", out var causeMsg))
                            description = causeMsg.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(description))
                        parts.Add($"{code ?? "?"}:{description ?? "?"}");
                }

                causes = parts.Count > 0 ? string.Join(" | ", parts) : "(vazio)";
            }

            _logger.LogWarning(
                "MercadoPago erro seguro:\nHttpStatus={HttpStatus}\nError={Error}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}",
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
                "MercadoPago erro seguro:\nHttpStatus={HttpStatus}\nError={Error}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nWwwAuthenticate={WwwAuthenticate}\nRequestId={RequestId}\nJsonParse=nao foi possivel interpretar a resposta (conteudo bruto nao registrado)",
                httpStatus,
                error,
                message,
                responseStatus,
                causes,
                wwwAuthenticate,
                requestId);
        }
    }

    /// <summary>
    /// Diagnóstico temporário do Access Token — nunca registra o valor completo.
    /// </summary>
    private void LogSafeAccessTokenDiagnostic(
        string trimmedAccessToken,
        string absoluteUrl,
        string? authorizationScheme)
    {
        var hashPartial = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmedAccessToken)))
            .ToLowerInvariant()[..12];
        var prefixoValido = trimmedAccessToken.StartsWith("APP_USR-", StringComparison.Ordinal);
        var fonte = string.IsNullOrWhiteSpace(_options.AccessTokenSource)
            ? "(desconhecida)"
            : _options.AccessTokenSource;
        var scheme = authorizationScheme ?? "(ausente)";

        // Somente metadados seguros — nunca o Access Token.
        _logger.LogWarning(
            "MercadoPago diagnóstico:\nFonte={Fonte}\nTamanho={Tamanho}\nPrefixoValido={PrefixoValido}\nHashParcial={HashParcial}\nUrl={Url}\nAuthorizationScheme={AuthorizationScheme}",
            fonte,
            trimmedAccessToken.Length,
            prefixoValido,
            hashPartial,
            absoluteUrl,
            scheme);
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
