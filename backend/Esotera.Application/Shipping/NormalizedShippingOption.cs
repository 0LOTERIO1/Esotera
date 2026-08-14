namespace Esotera.Application.Shipping;

/// <summary>
/// Opção de frete normalizada (J3 ou Melhor Envio). Sem tokens nem payload bruto ME.
/// </summary>
public sealed class NormalizedShippingOption
{
    public required string ShippingMethodId { get; init; }
    public required string Provider { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    public int? CompanyId { get; init; }
    public int? ServiceId { get; init; }
    public string? CarrierName { get; init; }
    public string? ServiceName { get; init; }

    /// <summary>Preço real da transportadora (antes de frete grátis/subsídio).</summary>
    public decimal OriginalPrice { get; init; }

    /// <summary>Preço cobrado do cliente (após frete grátis/subsídio).</summary>
    public decimal FinalPrice { get; init; }

    /// <summary>Mínimo em dias úteis; null = prazo desconhecido (nunca usar 0 como sentinel).</summary>
    public int? EstimatedDaysMin { get; init; }

    /// <summary>Máximo em dias úteis; null = prazo desconhecido (nunca usar 0 como sentinel).</summary>
    public int? EstimatedDaysMax { get; init; }

    public bool FreeShippingApplied { get; init; }
    public bool SubsidyApplied { get; init; }

    /// <summary>sandbox | production — ambiente da cotação ME; null para J3.</summary>
    public string? QuoteEnvironment { get; init; }

    public DateTime QuotedAtUtc { get; init; }

    public string EstimatedDaysLabel
    {
        get
        {
            if (EstimatedDaysMin is null || EstimatedDaysMax is null)
                return "Prazo a confirmar";

            if (EstimatedDaysMin == EstimatedDaysMax)
            {
                return EstimatedDaysMin.Value switch
                {
                    0 => "Hoje (até o fim do dia)",
                    1 => "1 dia útil",
                    _ => $"{EstimatedDaysMin.Value} dias úteis"
                };
            }

            return $"{EstimatedDaysMin.Value} a {EstimatedDaysMax.Value} dias úteis";
        }
    }
}
