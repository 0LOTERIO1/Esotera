using Esotera.Application.Interfaces;
using Esotera.Domain.Enums;

namespace Esotera.Application.Shipping;

/// <summary>
/// Mapeia serviços Melhor Envio (company/service IDs) → métodos internos.
/// Correios PAC (1,1) → melhor_economico; SEDEX (1,2) → melhor_expresso.
/// </summary>
public static class MelhorEnvioQuoteMapper
{
    public const int CorreiosCompanyId = 1;
    public const int PacServiceId = 1;
    public const int SedexServiceId = 2;
    public const string ServicesQuery = "1,2";

    public static string? MapToShippingMethodId(int? companyId, int? serviceId)
    {
        if (companyId != CorreiosCompanyId || serviceId is null)
            return null;

        return serviceId switch
        {
            PacServiceId => ShippingMethod.MelhorEconomico,
            SedexServiceId => ShippingMethod.MelhorExpresso,
            _ => null
        };
    }

    /// <summary>
    /// Prefer custom_price / custom_delivery_time quando presentes e válidos.
    /// Preço ou prazo ausente/inválido ⇒ opção indisponível (null).
    /// </summary>
    public static NormalizedShippingOption? TryMapService(
        MelhorEnvioRawServiceQuote raw,
        DateTime quotedAtUtc,
        string quoteEnvironment)
    {
        if (raw.HasError)
            return null;

        var methodId = MapToShippingMethodId(raw.CompanyId, raw.ServiceId);
        if (methodId is null)
            return null;

        if (!TryResolvePrice(raw, out var price))
            return null;

        if (!TryResolveDeliveryDays(raw, out var days))
            return null;

        return new NormalizedShippingOption
        {
            ShippingMethodId = methodId,
            Provider = ShippingMethod.GetProvider(methodId),
            Name = methodId == ShippingMethod.MelhorEconomico ? "Econômico" : "Expresso",
            Description = methodId == ShippingMethod.MelhorEconomico
                ? "Correios PAC via Melhor Envio"
                : "Correios SEDEX via Melhor Envio",
            CompanyId = raw.CompanyId,
            ServiceId = raw.ServiceId,
            CarrierName = string.IsNullOrWhiteSpace(raw.CompanyName) ? "Correios" : raw.CompanyName.Trim(),
            ServiceName = string.IsNullOrWhiteSpace(raw.ServiceName)
                ? (methodId == ShippingMethod.MelhorEconomico ? "PAC" : "SEDEX")
                : raw.ServiceName.Trim(),
            OriginalPrice = price,
            FinalPrice = price,
            EstimatedDaysMin = days,
            EstimatedDaysMax = days,
            FreeShippingApplied = false,
            SubsidyApplied = false,
            QuoteEnvironment = quoteEnvironment,
            QuotedAtUtc = quotedAtUtc
        };
    }

    public static bool TryResolvePrice(MelhorEnvioRawServiceQuote raw, out decimal price)
    {
        price = 0;
        if (raw.CustomPrice is decimal custom && custom >= 0)
        {
            price = decimal.Round(custom, 2, MidpointRounding.AwayFromZero);
            return true;
        }

        if (raw.Price is decimal p && p >= 0)
        {
            price = decimal.Round(p, 2, MidpointRounding.AwayFromZero);
            return true;
        }

        return false;
    }

    public static bool TryResolveDeliveryDays(MelhorEnvioRawServiceQuote raw, out int days)
    {
        days = 0;
        if (raw.CustomDeliveryTime is int custom && custom >= 0)
        {
            days = custom;
            return true;
        }

        if (raw.DeliveryTime is int d && d >= 0)
        {
            days = d;
            return true;
        }

        return false;
    }
}
