namespace Esotera.Application.Shipping;

/// <summary>
/// Valor de mercadorias para futura integração J3.
/// REGRA COMERCIAL ESOTERA (não é afirmação de regra oficial J3):
/// MerchandiseValue = max(0, Subtotal - Discount) — sem frete (ShippingPrice).
/// Antes da homologação mutativa real, confirmar com a J3 se aceitam este critério.
/// Não monta payload J3 neste passo.
/// </summary>
public static class J3MerchandiseValue
{
    /// <summary>Converte Subtotal−Discount (sem frete) para centavos (≥ 0).</summary>
    public static int ToCents(decimal subtotal, decimal discount)
    {
        var reais = Math.Max(0m, subtotal - discount);
        return (int)Math.Round(reais * 100m, MidpointRounding.AwayFromZero);
    }
}
