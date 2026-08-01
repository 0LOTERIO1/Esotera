using Esotera.Application.Common;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Frete simulado (J3 + Melhor Envio com valores fixos). Consome StoreSettings.
/// TODO: substituir faixas J3 pela cobertura oficial quando fornecida.
/// Preferir <see cref="IShippingQuoteService"/> / <see cref="ShippingQuoteService"/> no DI.
/// </summary>
public interface ISimulatedShippingService
{
    (decimal Price, int EstimatedDays) Quote(
        string shippingMethodId,
        string cep,
        string state,
        decimal productsTotalAfterDiscount,
        StoreSettings settings);
}

public sealed class SimulatedShippingService : ISimulatedShippingService, IShippingQuoteService
{
    private readonly IClock _clock;

    private static readonly (string Start, string End)[] SimulatedJ3CepRanges =
    [
        ("01000000", "05999999"),
        ("08000000", "08499999"),
        ("04000000", "04999999"),
    ];

    private static readonly HashSet<string> Sudeste =
        new(StringComparer.OrdinalIgnoreCase) { "SP", "RJ", "MG", "ES" };

    private static readonly HashSet<string> Sul =
        new(StringComparer.OrdinalIgnoreCase) { "PR", "SC", "RS" };

    public SimulatedShippingService(IClock clock)
    {
        _clock = clock;
    }

    public (decimal Price, int EstimatedDays) Quote(
        string shippingMethodId,
        string cep,
        string state,
        decimal productsTotalAfterDiscount,
        StoreSettings settings)
    {
        if (!ShippingMethod.IsValid(shippingMethodId))
            throw new ValidationException("shippingMethodId", "Método de envio inválido.");

        var normalizedState = state.Trim().ToUpperInvariant();
        var digits = new string(cep.Where(char.IsDigit).ToArray());
        var freeStates = FreeShippingStatesParser.Parse(settings.FreeShippingStatesCsv);

        if (shippingMethodId == ShippingMethod.J3)
            EnsureJ3Available(digits, settings);

        var free = productsTotalAfterDiscount >= settings.FreeShippingMin
            && freeStates.Contains(normalizedState, StringComparer.OrdinalIgnoreCase);

        if (free)
            return (0m, EstimateDays(shippingMethodId, normalizedState, settings));

        var basePrice = shippingMethodId switch
        {
            ShippingMethod.J3 => settings.J3Price,
            ShippingMethod.MelhorEconomico => MelhorEnvioPrice(normalizedState, express: false),
            ShippingMethod.MelhorExpresso => MelhorEnvioPrice(normalizedState, express: true),
            _ => throw new ValidationException("shippingMethodId", "Método de envio inválido.")
        };

        if (settings.ShippingSubsidyEnabled && basePrice > 0)
            basePrice = Math.Max(0, basePrice - settings.ShippingSubsidyAmount);

        return (Math.Max(0, basePrice), EstimateDays(shippingMethodId, normalizedState, settings));
    }

    private void EnsureJ3Available(string cepDigits, StoreSettings settings)
    {
        if (cepDigits.Length != 8 || !IsJ3CepEligible(cepDigits))
            throw new ValidationException(
                "shippingMethodId",
                "Modalidade J3 não disponível para este CEP.");

        var spNow = GetSaoPauloLocalTime(_clock.UtcNow);
        if (!J3WorkingDays.IsWorkingDay(spNow))
            throw new ValidationException(
                "shippingMethodId",
                "Modalidade J3 disponível apenas de segunda a sexta-feira.");
    }

    public static bool IsJ3CepEligible(string cepDigits)
    {
        return SimulatedJ3CepRanges.Any(r =>
            string.CompareOrdinal(cepDigits, r.Start) >= 0
            && string.CompareOrdinal(cepDigits, r.End) <= 0);
    }

    private int EstimateDays(string methodId, string state, StoreSettings settings)
    {
        if (methodId == ShippingMethod.J3)
        {
            var spNow = GetSaoPauloLocalTime(_clock.UtcNow);
            var cutoff = settings.J3CutoffHour is >= 0 and <= 23 ? settings.J3CutoffHour : 12;
            return spNow.Hour < cutoff ? 0 : 1;
        }

        var region = Sudeste.Contains(state) ? "Sudeste"
            : Sul.Contains(state) ? "Sul" : "Outros";

        return ShippingMethod.GetEstimatedDays(methodId, region);
    }

    private static decimal MelhorEnvioPrice(string state, bool express)
    {
        if (state == "SP")
            return express ? 28.90m : 18.90m;
        if (Sudeste.Contains(state))
            return express ? 36.90m : 24.90m;
        if (Sul.Contains(state))
            return express ? 42.90m : 29.90m;
        return express ? 59.90m : 39.90m;
    }

    public static DateTime GetSaoPauloLocalTime(DateTime utcNow)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        }
    }
}
