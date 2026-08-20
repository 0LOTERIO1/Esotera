namespace Esotera.Application.Options;

/// <summary>
/// Configuração operacional UpSeller (exportação de pedidos). Sem secrets.
/// Valores alinhados à homologação: Loja Padrão / My Warehouse / método 2 / NF-e Não.
/// </summary>
public sealed class UpSellerOptions
{
    public const string SectionName = "UpSeller";

    public string StoreName { get; set; } = "Loja Padrão";
    public string WarehouseName { get; set; } = "My Warehouse";

    /// <summary>Código numérico do método de custo de envio no UpSeller (0/1/2/3/4/9).</summary>
    public string ShippingCostMethod { get; set; } = "2";

    public int PackageQuantity { get; set; } = 1;

    /// <summary>Valor exato da lista suspensa UpSeller: "Não" ou "Sim". Nesta fase: Não.</summary>
    public string InvoiceRequired { get; set; } = "Não";

    /// <summary>Fallback quando PaymentMethodMap não cobre o método do pedido.</summary>
    public string DefaultPaymentMethod { get; set; } = "Dinheiro";

    /// <summary>Mapa pix/card/boleto → rótulo aceito pelo UpSeller.</summary>
    public Dictionary<string, string> PaymentMethodMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pix"] = "PIX",
        ["card"] = "Cartão de Crédito",
        ["boleto"] = "Outros"
    };

    public string ResolvePaymentMethod(string? paymentMethod)
    {
        if (!string.IsNullOrWhiteSpace(paymentMethod)
            && PaymentMethodMap.TryGetValue(paymentMethod.Trim(), out var mapped)
            && !string.IsNullOrWhiteSpace(mapped))
        {
            return mapped.Trim();
        }

        return string.IsNullOrWhiteSpace(DefaultPaymentMethod) ? "Dinheiro" : DefaultPaymentMethod.Trim();
    }
}
