using System.Text;

namespace Esotera.Application.Shipping;

/// <summary>
/// Códigos curtos e sanitizados para MelhorEnvioShipment.LastSyncErrorCode.
/// NUNCA conter PII, payload, chave de NF-e ou token.
/// </summary>
public static class MelhorEnvioShipmentErrorCodes
{
    // Bloqueios locais — nenhuma chamada HTTP foi feita.
    public const string ShipmentMissing = "SHIPMENT_MISSING";
    public const string OrderMissing = "ORDER_MISSING";
    public const string NotMelhorEnvioShipping = "NOT_MELHOR_ENVIO_SHIPPING";
    public const string PaymentNotApproved = "PAYMENT_NOT_APPROVED";
    public const string InvoiceNotAuthorized = "INVOICE_NOT_AUTHORIZED";
    public const string InvoiceKeyMissing = "INVOICE_KEY_MISSING";
    public const string StatusNotReady = "STATUS_NOT_READY";
    public const string AlreadyCreated = "ALREADY_CREATED";
    public const string ClaimLost = "CLAIM_LOST";
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string EnvironmentMismatch = "ENVIRONMENT_MISMATCH";
    public const string ScopeMissing = "SCOPE_MISSING";
    public const string SenderIncomplete = "SENDER_INCOMPLETE";
    public const string OriginCepMissing = "ORIGIN_CEP_MISSING";
    public const string ServiceIdMissing = "SERVICE_ID_MISSING";
    public const string RecipientIncomplete = "RECIPIENT_INCOMPLETE";
    public const string ItemsMissing = "ITEMS_MISSING";
    public const string PackageInvalid = "PACKAGE_INVALID";

    // Resultados da chamada ao Melhor Envio.
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string ValidationRejected = "VALIDATION_REJECTED";
    public const string ResponseWithoutId = "RESPONSE_WITHOUT_ID";
    public const string InvalidJson = "INVALID_JSON";
    public const string Timeout = "TIMEOUT_UNKNOWN";
    public const string NetworkError = "NETWORK_ERROR";
    public const string TokenUnavailable = "TOKEN_UNAVAILABLE";
    public const string Unexpected = "UNEXPECTED";

    public static string Http(int statusCode) => $"HTTP_{statusCode}";

    /// <summary>Mantém apenas A-Z, 0-9 e underscore; trunca em 64.</summary>
    public static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Unexpected;

        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw.Trim().ToUpperInvariant())
        {
            if (ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9' || ch == '_')
                sb.Append(ch);
            else if (ch is ' ' or '-' or '.')
                sb.Append('_');
        }

        var result = sb.ToString().Trim('_');
        if (result.Length == 0)
            return Unexpected;

        return result.Length > 64 ? result[..64] : result;
    }

    /// <summary>Mensagem operacional para o Admin. Truncada e sem payload bruto.</summary>
    public static string SanitizeMessage(string? raw, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Erro não identificado.";

        var collapsed = string.Join(' ', raw.Split(
            [' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length > maxLength ? collapsed[..maxLength] : collapsed;
    }
}
