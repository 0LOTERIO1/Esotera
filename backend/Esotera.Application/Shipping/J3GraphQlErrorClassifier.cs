using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Esotera.Application.Shipping;

/// <summary>
/// Classificação e sanitização de errors[] GraphQL J3 — sem token/XML/PII.
/// </summary>
public static class J3GraphQlErrorClassifier
{
    private static readonly HashSet<string> PreExecutionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "GRAPHQL_PARSE_FAILED",
        "GRAPHQL_VALIDATION_FAILED"
    };

    private static readonly HashSet<string> AuthCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNAUTHENTICATED",
        "FORBIDDEN",
        "UNAUTHORIZED"
    };

    public sealed record Insight(string? Code, string? SanitizedMessage);

    public static IReadOnlyList<Insight> Extract(JsonElement errors)
    {
        if (errors.ValueKind != JsonValueKind.Array)
            return Array.Empty<Insight>();

        var list = new List<Insight>();
        foreach (var err in errors.EnumerateArray())
        {
            if (err.ValueKind != JsonValueKind.Object)
                continue;
            list.Add(new Insight(ReadCode(err), SanitizeMessage(ReadMessage(err))));
        }

        return list;
    }

    public static bool AllPreExecution(JsonElement errors)
    {
        var any = false;
        foreach (var insight in Extract(errors))
        {
            any = true;
            if (insight.Code is null || !PreExecutionCodes.Contains(insight.Code))
                return false;
        }

        return any;
    }

    public static bool AllAuthRejected(JsonElement errors)
    {
        var any = false;
        foreach (var insight in Extract(errors))
        {
            any = true;
            if (insight.Code is null || !AuthCodes.Contains(insight.Code))
                return false;
        }

        return any;
    }

    public static string PrimaryErrorCode(IReadOnlyList<Insight> insights)
    {
        foreach (var i in insights)
        {
            var sanitized = J3FulfillmentErrorCodes.Sanitize(i.Code);
            if (!string.IsNullOrWhiteSpace(sanitized))
                return sanitized;
        }

        return J3FulfillmentErrorCodes.GraphqlAmbiguous;
    }

    public static string AuthFailureCode(IReadOnlyList<Insight> insights)
    {
        foreach (var i in insights)
        {
            if (i.Code is null) continue;
            if (i.Code.Equals("UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase)
                || i.Code.Equals("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
            {
                return J3FulfillmentErrorCodes.GraphqlUnauthenticated;
            }

            if (i.Code.Equals("FORBIDDEN", StringComparison.OrdinalIgnoreCase))
                return J3FulfillmentErrorCodes.GraphqlForbidden;
        }

        return J3FulfillmentErrorCodes.GraphqlUnauthenticated;
    }

    public static string? ReadCode(JsonElement err)
    {
        if (err.TryGetProperty("extensions", out var ext)
            && ext.ValueKind == JsonValueKind.Object
            && ext.TryGetProperty("code", out var codeEl)
            && codeEl.ValueKind == JsonValueKind.String)
        {
            return codeEl.GetString();
        }

        if (err.TryGetProperty("code", out var top) && top.ValueKind == JsonValueKind.String)
            return top.GetString();

        return null;
    }

    public static string? ReadMessage(JsonElement err)
    {
        if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            return msg.GetString();
        return null;
    }

    /// <summary>
    /// Mantém mensagem curta sem dígitos longos (CPF/CNPJ/chaves), emails ou Bearer.
    /// </summary>
    public static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var s = message.Trim();
        if (s.Contains("Bearer", StringComparison.OrdinalIgnoreCase)
            || s.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            return "REDACTED";
        }

        s = Regex.Replace(s, @"\b\d{8,}\b", "…");
        s = Regex.Replace(s, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "…", RegexOptions.IgnoreCase);

        var sb = new StringBuilder(Math.Min(s.Length, 120));
        foreach (var ch in s)
        {
            if (sb.Length >= 120)
                break;
            if (char.IsControl(ch))
                continue;
            sb.Append(ch);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }
}
