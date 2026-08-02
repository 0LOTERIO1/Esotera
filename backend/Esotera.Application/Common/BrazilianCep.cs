using System.Text.RegularExpressions;

namespace Esotera.Application.Common;

/// <summary>Normalização e validação de CEP brasileiro (8 dígitos).</summary>
public static partial class BrazilianCep
{
    public const int DigitLength = 8;

    /// <summary>Extrai apenas dígitos. Retorna null se não tiver exatamente 8.</summary>
    public static string? TryNormalize(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep))
            return null;

        var digits = DigitsOnly().Replace(cep, "");
        return digits.Length == DigitLength ? digits : null;
    }

    public static bool IsValid(string? cep) => TryNormalize(cep) is not null;

    /// <summary>Formato XXXXX-XXX para APIs que esperam máscara.</summary>
    public static string FormatMasked(string eightDigits)
    {
        if (eightDigits.Length != DigitLength)
            throw new ArgumentException("CEP deve ter 8 dígitos.", nameof(eightDigits));
        return $"{eightDigits[..5]}-{eightDigits[5..]}";
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
