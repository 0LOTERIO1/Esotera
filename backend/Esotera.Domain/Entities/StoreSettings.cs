namespace Esotera.Domain.Entities;

public class StoreSettings
{
    public int Id { get; set; } = 1;
    public string StoreName { get; set; } = "Esotera";
    public decimal FreeShippingMin { get; set; } = 99.90m;
    public string FreeShippingStatesCsv { get; set; } = "SP,RJ,MG,ES,PR,SC,RS";
    public decimal J3Price { get; set; } = 12.00m;
    public int J3CutoffHour { get; set; } = 12;
    /// <summary>LEGADO — não usar nas regras comerciais. Fonte oficial: tabela Coupons.</summary>
    [Obsolete("Legado. Use Coupons.DiscountAmount.")]
    public decimal CouponDiscount { get; set; } = 5.00m;
    /// <summary>LEGADO — não usar nas regras comerciais. Fonte oficial: tabela Coupons.</summary>
    [Obsolete("Legado. Use Coupons.MinPurchase.")]
    public decimal CouponMinPurchase { get; set; } = 30.00m;
    public bool ShippingSubsidyEnabled { get; set; }
    public decimal ShippingSubsidyAmount { get; set; } = 10.00m;

    /// <summary>CEP de origem para cotação Melhor Envio (8 dígitos ou mascarado).</summary>
    public string ShippingOriginCep { get; set; } = "08061420";
    public decimal PackageLengthCm { get; set; } = 16m;
    public decimal PackageWidthCm { get; set; } = 11m;
    public decimal PackageHeightCm { get; set; } = 6m;
    public int PackageWeightGrams { get; set; } = 400;

    /// <summary>
    /// Cotação Melhor Envio ativa — independente de MELHOR_ENVIO_ENABLED e do status OAuth.
    /// Inicia desativada.
    /// </summary>
    public bool MelhorEnvioQuoteEnabled { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
