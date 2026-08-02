using Esotera.Application.Common;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>Regras de frete grátis e subsídio sobre preço real da transportadora.</summary>
public static class ShippingCommerceRules
{
    public static NormalizedShippingOption Apply(
        NormalizedShippingOption option,
        decimal productsTotalAfterDiscount,
        string state,
        StoreSettings settings)
    {
        var freeStates = FreeShippingStatesParser.Parse(settings.FreeShippingStatesCsv);
        var normalizedState = state.Trim().ToUpperInvariant();
        var free = productsTotalAfterDiscount >= settings.FreeShippingMin
            && freeStates.Contains(normalizedState, StringComparer.OrdinalIgnoreCase);

        if (free)
        {
            return new NormalizedShippingOption
            {
                ShippingMethodId = option.ShippingMethodId,
                Provider = option.Provider,
                Name = option.Name,
                Description = option.Description,
                CompanyId = option.CompanyId,
                ServiceId = option.ServiceId,
                CarrierName = option.CarrierName,
                ServiceName = option.ServiceName,
                OriginalPrice = option.OriginalPrice,
                FinalPrice = 0m,
                EstimatedDaysMin = option.EstimatedDaysMin,
                EstimatedDaysMax = option.EstimatedDaysMax,
                FreeShippingApplied = true,
                SubsidyApplied = false,
                QuoteEnvironment = option.QuoteEnvironment,
                QuotedAtUtc = option.QuotedAtUtc
            };
        }

        var final = option.OriginalPrice;
        var subsidy = false;
        if (settings.ShippingSubsidyEnabled && final > 0)
        {
            var reduced = Math.Max(0, final - settings.ShippingSubsidyAmount);
            subsidy = reduced < final;
            final = reduced;
        }

        return new NormalizedShippingOption
        {
            ShippingMethodId = option.ShippingMethodId,
            Provider = option.Provider,
            Name = option.Name,
            Description = option.Description,
            CompanyId = option.CompanyId,
            ServiceId = option.ServiceId,
            CarrierName = option.CarrierName,
            ServiceName = option.ServiceName,
            OriginalPrice = option.OriginalPrice,
            FinalPrice = Math.Max(0, final),
            EstimatedDaysMin = option.EstimatedDaysMin,
            EstimatedDaysMax = option.EstimatedDaysMax,
            FreeShippingApplied = false,
            SubsidyApplied = subsidy,
            QuoteEnvironment = option.QuoteEnvironment,
            QuotedAtUtc = option.QuotedAtUtc
        };
    }
}
