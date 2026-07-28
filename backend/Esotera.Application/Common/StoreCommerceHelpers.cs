using System.Text.RegularExpressions;

namespace Esotera.Application.Common;

public static class CouponCodeNormalizer
{
    public static string Normalize(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();
}

public static class FreeShippingStatesParser
{
    private static readonly Regex UfRegex = new("^[A-Z]{2}$", RegexOptions.Compiled);

    /// <summary>UFs brasileiras válidas (ISO 3166-2:BR).</summary>
    private static readonly HashSet<string> ValidUfs = new(StringComparer.Ordinal)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
        "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public static IReadOnlyList<string> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<string>();

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .Where(s => ValidUfs.Contains(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string NormalizeToCsv(IEnumerable<string> states) =>
        string.Join(",", Parse(string.Join(",", states)));

    public static bool TryValidate(IEnumerable<string> states, out string? error, out string normalizedCsv)
    {
        var list = states
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (list.Count == 0)
        {
            error = "Informe ao menos um estado elegível ao frete grátis.";
            normalizedCsv = string.Empty;
            return false;
        }

        var invalidFormat = list.Where(s => !UfRegex.IsMatch(s)).Distinct().ToArray();
        if (invalidFormat.Length > 0)
        {
            error = $"Siglas inválidas: {string.Join(", ", invalidFormat)}.";
            normalizedCsv = string.Empty;
            return false;
        }

        var invalidUf = list.Where(s => !ValidUfs.Contains(s)).Distinct().ToArray();
        if (invalidUf.Length > 0)
        {
            error = $"Siglas inválidas: {string.Join(", ", invalidUf)}.";
            normalizedCsv = string.Empty;
            return false;
        }

        normalizedCsv = string.Join(",", list.Distinct(StringComparer.Ordinal));
        error = null;
        return true;
    }
}

/// <summary>Regra centralizada: J3 opera de segunda a sexta (America/Sao_Paulo).</summary>
public static class J3WorkingDays
{
    public static bool IsWorkingDay(DateTime saoPauloLocal) =>
        saoPauloLocal.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
