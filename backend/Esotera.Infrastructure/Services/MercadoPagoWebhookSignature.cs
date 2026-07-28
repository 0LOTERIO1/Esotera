using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Validação HMAC-SHA256 do header x-signature (Webhooks Mercado Pago).
/// </summary>
public static class MercadoPagoWebhookSignature
{
    public static bool IsValid(
        string? xSignature,
        string? xRequestId,
        string? dataId,
        string? secret,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Secret não configurado: caller decide se aceita (ambiente de teste inicial).
            return true;
        }

        if (string.IsNullOrWhiteSpace(xSignature))
        {
            logger?.LogWarning("Webhook MP rejeitado: x-signature ausente.");
            return false;
        }

        string? ts = null;
        string? v1 = null;
        foreach (var part in xSignature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            if (kv[0] == "v1") v1 = kv[1];
        }

        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(v1))
        {
            logger?.LogWarning("Webhook MP rejeitado: x-signature malformada.");
            return false;
        }

        var manifest = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(dataId))
            manifest.Append(CultureInfo.InvariantCulture, $"id:{dataId.ToLowerInvariant()};");
        if (!string.IsNullOrWhiteSpace(xRequestId))
            manifest.Append(CultureInfo.InvariantCulture, $"request-id:{xRequestId};");
        manifest.Append(CultureInfo.InvariantCulture, $"ts:{ts};");

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(manifest.ToString());
        var hash = HMACSHA256.HashData(keyBytes, msgBytes);
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));

        if (!ok)
            logger?.LogWarning("Webhook MP rejeitado: assinatura inválida.");

        return ok;
    }

    public static string? ExtractDataIdFromBody(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("id", out var id))
            {
                return id.ToString();
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
