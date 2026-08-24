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
/// Cliente HTTP da Orders API (Checkout Transparente).
/// Pix, crédito, débito e boleto via POST /v1/orders.
/// </summary>
public class MercadoPagoHttpClient : IMercadoPagoClient
{
    private static readonly HashSet<string> RedactedJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "qr_code",
        "qr_code_base64",
        "token",
        "access_token",
        "password",
        "secret",
        "authorization",
        "card_number",
        "security_code",
        "cvv",
        "identification",
        "number",
        "email",
        "first_name",
        "last_name",
        "phone",
        "digitable_line",
        "barcode_content"
    };

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

        var methodType = (command.PaymentMethodType ?? "").Trim().ToLowerInvariant();
        var amount = FormatAmount(command.TransactionAmount);
        var paymentMethod = BuildPaymentMethodNode(command, methodType);

        var paymentTx = new Dictionary<string, object?>
        {
            ["amount"] = amount,
            ["payment_method"] = paymentMethod,
        };

        if (methodType == "ticket")
            paymentTx["expiration_time"] = "P3D";

        var body = new Dictionary<string, object?>
        {
            ["type"] = "online",
            ["processing_mode"] = "automatic",
            ["external_reference"] = command.ExternalReference,
            ["total_amount"] = amount,
            ["payer"] = BuildPayer(command, methodType),
            ["transactions"] = new Dictionary<string, object?>
            {
                ["payments"] = new object[] { paymentTx },
            },
        };

        if ((methodType is "ticket" or "bank_transfer") && !command.IsSandboxOfficialTest)
        {
            var shipment = BuildShipmentAddress(command);
            if (shipment is not null)
                body["shipment"] = shipment;
        }

        if (!command.IsSandboxOfficialTest && !string.IsNullOrWhiteSpace(command.Description))
            body["description"] = command.Description;

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
            var parsed = LogSafeApiError("CreateOrder", response, raw);
            throw MapCreateOrderFailure(parsed, methodType);
        }

        var snapshot = ParseOrder(raw);
        _logger.LogInformation(
            "Mercado Pago order criada: OrderId={OrderId} PaymentId={PaymentId} Status={Status} StatusDetail={StatusDetail} MethodType={MethodType} ExternalReferencePrefix={ExternalReferencePrefix}",
            snapshot.OrderId,
            snapshot.TransactionPaymentId ?? "(ausente)",
            snapshot.Status,
            snapshot.StatusDetail,
            methodType,
            snapshot.ExternalReference.Length >= 24
                ? snapshot.ExternalReference[..24]
                : snapshot.ExternalReference);
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

    /// <summary>
    /// Mapeia Brick/request → nó payment_method da Orders API.
    /// bolbradesco (Brick) → id=boleto, type=ticket.
    /// </summary>
    internal static Dictionary<string, object?> BuildPaymentMethodNode(
        MercadoPagoCreatePaymentCommand command,
        string methodType)
    {
        var brickId = (command.PaymentMethodId ?? "").Trim().ToLowerInvariant();

        return methodType switch
        {
            "bank_transfer" => new Dictionary<string, object?>
            {
                ["id"] = "pix",
                ["type"] = "bank_transfer",
            },
            "credit_card" => BuildCardPaymentMethod(
                brickId,
                "credit_card",
                command.Token,
                installments: command.Installments ?? 1,
                issuerId: command.IssuerId),
            "debit_card" => BuildCardPaymentMethod(
                brickId,
                "debit_card",
                command.Token,
                installments: null,
                issuerId: command.IssuerId),
            "ticket" => new Dictionary<string, object?>
            {
                // Brick: bolbradesco → Orders: boleto + ticket
                ["id"] = MapTicketPaymentMethodId(brickId),
                ["type"] = "ticket",
            },
            _ => throw new ValidationException(
                "paymentMethodType",
                "Tipo de pagamento não suportado."),
        };
    }

    private static Dictionary<string, object?> BuildCardPaymentMethod(
        string methodId,
        string type,
        string? token,
        int? installments,
        string? issuerId)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("token", "Token do cartão é obrigatório.");
        if (string.IsNullOrWhiteSpace(methodId))
            throw new ValidationException("paymentMethodId", "Método de pagamento inválido.");

        var node = new Dictionary<string, object?>
        {
            ["id"] = methodId,
            ["type"] = type,
            ["token"] = token.Trim(),
        };

        if (installments is > 0)
            node["installments"] = installments.Value;

        // Issuer só quando o Brick enviar (não inventar).
        if (!string.IsNullOrWhiteSpace(issuerId))
            node["issuer_id"] = issuerId.Trim();

        return node;
    }

    /// <summary>Brick ticket id → Orders API id.</summary>
    public static string MapTicketPaymentMethodId(string brickOrOrdersId) =>
        brickOrOrdersId switch
        {
            "bolbradesco" => "boleto",
            "boleto" => "boleto",
            _ => throw new ValidationException(
                "paymentMethodId",
                "Método de boleto não suportado."),
        };

    private ValidationException MapCreateOrderFailure(SafeMpErrorParsed parsed, string methodType)
    {
        var code = (parsed.Code ?? parsed.Error ?? "").Trim().ToLowerInvariant();
        if (code.Contains("invalid_email_for_sandbox", StringComparison.Ordinal)
            || (parsed.Message?.Contains("invalid_email_for_sandbox", StringComparison.OrdinalIgnoreCase) ?? false)
            || (parsed.Message?.Contains("@testuser.com", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return new ValidationException("payment", MercadoPagoOptions.CommercialSandboxBlockedMessage);
        }

        var label = methodType switch
        {
            "credit_card" or "debit_card" => "cartão",
            "ticket" => "boleto",
            _ => "Pix",
        };

        return new ValidationException(
            "payment",
            $"Não foi possível criar o pagamento ({label}). Verifique os dados e tente novamente.");
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

    private sealed record SafeMpErrorParsed(
        int HttpStatus,
        string? Error,
        string? Code,
        string? Message,
        string? ResponseStatus,
        string Causes,
        string RequestId,
        string SanitizedBody);

    private SafeMpErrorParsed LogSafeApiError(string operation, HttpResponseMessage response, string rawBody)
    {
        var httpStatus = (int)response.StatusCode;
        var requestId = "(ausente)";
        if (response.Headers.TryGetValues("x-request-id", out var reqIds))
            requestId = reqIds.FirstOrDefault() ?? "(ausente)";

        string? error = null;
        string? code = null;
        string? message = null;
        string? responseStatus = null;
        var causes = "(ausente)";
        var sanitized = "(corpo vazio)";

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                sanitized = SanitizeJsonElement(root);

                if (root.TryGetProperty("error", out var errorEl))
                    error = ReadStringish(errorEl);
                if (root.TryGetProperty("code", out var codeEl))
                    code = ReadStringish(codeEl);
                if (root.TryGetProperty("message", out var messageEl))
                    message = ReadStringish(messageEl);
                if (root.TryGetProperty("status", out var statusEl))
                    responseStatus = ReadStringish(statusEl);

                var causeParts = new List<string>();
                if (root.TryGetProperty("cause", out var causeEl)
                    && causeEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in causeEl.EnumerateArray())
                        AppendCause(causeParts, item);
                }

                if (root.TryGetProperty("errors", out var errorsEl)
                    && errorsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in errorsEl.EnumerateArray())
                        AppendCause(causeParts, item);
                }

                if (causeParts.Count > 0)
                    causes = string.Join(" | ", causeParts);

                error ??= code;
            }
            catch (JsonException)
            {
                sanitized = "(nao foi possivel interpretar a resposta; conteudo bruto nao registrado)";
            }
        }

        _logger.LogWarning(
            "MercadoPago erro seguro ({Operation}):\nHttpStatus={HttpStatus}\nError={Error}\nCode={Code}\nMessage={Message}\nResponseStatus={ResponseStatus}\nCauses={Causes}\nRequestId={RequestId}\nSanitizedBody={SanitizedBody}",
            operation,
            httpStatus,
            error ?? "(ausente)",
            code ?? "(ausente)",
            message ?? "(ausente)",
            responseStatus ?? "(ausente)",
            causes,
            requestId,
            sanitized);

        return new SafeMpErrorParsed(
            httpStatus,
            error,
            code,
            message,
            responseStatus,
            causes,
            requestId,
            sanitized);
    }

    private static void AppendCause(List<string> parts, JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            parts.Add(item.GetString() ?? "?");
            return;
        }

        if (item.ValueKind != JsonValueKind.Object)
            return;

        string? c = null;
        string? d = null;
        if (item.TryGetProperty("code", out var codeEl))
            c = ReadStringish(codeEl);
        if (item.TryGetProperty("description", out var descEl))
            d = descEl.GetString();
        if (d is null && item.TryGetProperty("message", out var msgEl))
            d = msgEl.GetString();
        if (!string.IsNullOrWhiteSpace(c) || !string.IsNullOrWhiteSpace(d))
            parts.Add($"{c ?? "?"}:{d ?? "?"}");
    }

    private static string SanitizeJsonElement(JsonElement el)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteSanitized(writer, el, propertyName: null);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSanitized(Utf8JsonWriter writer, JsonElement el, string? propertyName)
    {
        if (propertyName is not null && RedactedJsonKeys.Contains(propertyName))
        {
            writer.WriteString(propertyName, "[REDACTED]");
            return;
        }

        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (propertyName is null) writer.WriteStartObject();
                else writer.WriteStartObject(propertyName);
                foreach (var prop in el.EnumerateObject())
                    WriteSanitized(writer, prop.Value, prop.Name);
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                if (propertyName is null) writer.WriteStartArray();
                else writer.WriteStartArray(propertyName);
                foreach (var item in el.EnumerateArray())
                    WriteSanitized(writer, item, propertyName: null);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                if (propertyName is null) writer.WriteStringValue(el.GetString());
                else writer.WriteString(propertyName, el.GetString());
                break;
            case JsonValueKind.Number:
                if (propertyName is null) writer.WriteRawValue(el.GetRawText());
                else { writer.WritePropertyName(propertyName); writer.WriteRawValue(el.GetRawText()); }
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (propertyName is null) writer.WriteBooleanValue(el.GetBoolean());
                else writer.WriteBoolean(propertyName, el.GetBoolean());
                break;
            case JsonValueKind.Null:
                if (propertyName is null) writer.WriteNullValue();
                else writer.WriteNull(propertyName);
                break;
            default:
                if (propertyName is null) writer.WriteStringValue(el.ToString());
                else writer.WriteString(propertyName, el.ToString());
                break;
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

    private static object BuildPayer(MercadoPagoCreatePaymentCommand command, string methodType)
    {
        var email = command.PayerEmail;
        var hasName = !string.IsNullOrWhiteSpace(command.PayerFirstName);
        var lastName = command.PayerLastName?.Trim();
        var cpf = DigitsOnly(command.PayerCpf);
        var hasCpf = cpf?.Length == 11;

        if (methodType == "ticket")
        {
            // Orders API: boleto exige identification (+ preferimos address do pedido).
            if (!hasCpf)
                throw new ValidationException(
                    "payerIdentification",
                    "CPF do pagador é obrigatório para boleto.");

            var payer = new Dictionary<string, object?>
            {
                ["email"] = email,
                ["identification"] = new { type = "CPF", number = cpf },
            };

            if (hasName)
                payer["first_name"] = command.PayerFirstName!.Trim();
            if (!string.IsNullOrWhiteSpace(lastName))
                payer["last_name"] = lastName;

            var address = BuildPayerAddress(command);
            if (address is not null)
                payer["address"] = address;

            return payer;
        }

        if (hasName && hasCpf)
        {
            var o = new Dictionary<string, object?>
            {
                ["email"] = email,
                ["first_name"] = command.PayerFirstName!.Trim(),
                ["identification"] = new { type = "CPF", number = cpf },
            };
            if (!string.IsNullOrWhiteSpace(lastName))
                o["last_name"] = lastName;
            return o;
        }

        if (hasName)
        {
            var o = new Dictionary<string, object?>
            {
                ["email"] = email,
                ["first_name"] = command.PayerFirstName!.Trim(),
            };
            if (!string.IsNullOrWhiteSpace(lastName))
                o["last_name"] = lastName;
            return o;
        }

        if (hasCpf)
        {
            return new Dictionary<string, object?>
            {
                ["email"] = email,
                ["identification"] = new { type = "CPF", number = cpf },
            };
        }

        return new Dictionary<string, object?> { ["email"] = email };
    }

    private static Dictionary<string, object?>? BuildPayerAddress(MercadoPagoCreatePaymentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.PayerZipCode)
            || string.IsNullOrWhiteSpace(command.PayerStreetName)
            || string.IsNullOrWhiteSpace(command.PayerStreetNumber)
            || string.IsNullOrWhiteSpace(command.PayerCity)
            || string.IsNullOrWhiteSpace(command.PayerState))
            return null;

        return new Dictionary<string, object?>
        {
            ["zip_code"] = DigitsOnly(command.PayerZipCode) ?? command.PayerZipCode.Trim(),
            ["street_name"] = command.PayerStreetName.Trim(),
            ["street_number"] = command.PayerStreetNumber.Trim(),
            ["neighborhood"] = command.PayerNeighborhood?.Trim() ?? "",
            ["city"] = command.PayerCity.Trim(),
            ["state"] = command.PayerState.Trim().Length >= 2
                ? command.PayerState.Trim()[..2].ToUpperInvariant()
                : command.PayerState.Trim(),
            ["complement"] = command.PayerComplement?.Trim() ?? "",
        };
    }

    private static Dictionary<string, object?>? BuildShipmentAddress(MercadoPagoCreatePaymentCommand command)
    {
        var address = BuildPayerAddress(command);
        return address is null ? null : new Dictionary<string, object?> { ["address"] = address };
    }

    private static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
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
        string? digitableLine = null;
        string? barcodeContent = null;

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
                    if (pm.TryGetProperty("id", out var pmid) && pmid.ValueKind == JsonValueKind.String)
                        methodId = pmid.GetString();
                    if (pm.TryGetProperty("qr_code", out var qr))
                        qrCode = qr.GetString();
                    if (pm.TryGetProperty("qr_code_base64", out var qrb))
                        qrBase64 = qrb.GetString();
                    if (pm.TryGetProperty("ticket_url", out var tu))
                        ticketUrl = tu.GetString();
                    if (pm.TryGetProperty("digitable_line", out var dl))
                        digitableLine = dl.GetString();
                    if (pm.TryGetProperty("barcode_content", out var bc))
                        barcodeContent = bc.GetString();
                }

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
            dateOfExpiration,
            digitableLine,
            barcodeContent);
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
