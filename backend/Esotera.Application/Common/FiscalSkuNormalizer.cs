namespace Esotera.Application.Common;

/// <summary>
/// Normalização SOMENTE para matching fiscal Order ↔ NF-e.
/// Não altera SKUs persistidos (catálogo/pedido).
/// UpSeller remove hífens em cProd (ex.: SKU-WAITE-TAROT → SKUWAITETAROT).
/// </summary>
public static class FiscalSkuNormalizer
{
    public static string Normalize(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return string.Empty;

        var s = sku.Trim().ToUpperInvariant();
        return new string(s.Where(char.IsLetterOrDigit).ToArray());
    }

    public static bool EqualsNormalized(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal)
        && Normalize(a).Length > 0;
}
