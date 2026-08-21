using System.Text;
using System.Text.Json;

namespace Esotera.Application.Shipping;

/// <summary>
/// Lê claim exp do JWT apenas para renovação de cache. NÃO valida assinatura/identidade.
/// </summary>
public static class J3JwtExpReader
{
    /// <summary>
    /// Retorna DateTimeOffset UTC do exp, ou null se ilegível.
    /// </summary>
    public static DateTimeOffset? TryReadExpiresAtUtc(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1];
            var padded = payload.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var expEl))
                return null;

            long exp;
            if (expEl.ValueKind == JsonValueKind.Number && expEl.TryGetInt64(out exp))
            { }
            else if (expEl.ValueKind == JsonValueKind.String
                     && long.TryParse(expEl.GetString(), out exp))
            { }
            else
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(exp);
        }
        catch
        {
            return null;
        }
    }
}
