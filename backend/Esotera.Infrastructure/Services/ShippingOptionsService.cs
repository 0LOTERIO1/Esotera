using Esotera.Application.Common;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cotação centralizada: J3 (regras locais) + Melhor Envio sandbox (quando flag ativa).
/// Sem fallback fictício ME. Falha ME não impede J3.
/// </summary>
public sealed class ShippingOptionsService : IShippingOptionsService
{
    private readonly IMelhorEnvioOAuthService _oauth;
    private readonly IMelhorEnvioShipmentClient _shipmentClient;
    private readonly MelhorEnvioOptions _meOptions;
    private readonly IClock _clock;
    private readonly ILogger<ShippingOptionsService> _logger;

    public ShippingOptionsService(
        IMelhorEnvioOAuthService oauth,
        IMelhorEnvioShipmentClient shipmentClient,
        IOptions<MelhorEnvioOptions> meOptions,
        IClock clock,
        ILogger<ShippingOptionsService> logger)
    {
        _oauth = oauth;
        _shipmentClient = shipmentClient;
        _meOptions = meOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NormalizedShippingOption>> GetAvailableOptionsAsync(
        ShippingQuoteQuery query,
        StoreSettings settings,
        CancellationToken cancellationToken = default)
    {
        var quotedAt = _clock.UtcNow;
        var options = new List<NormalizedShippingOption>();

        var j3 = TryBuildJ3Option(query, settings, quotedAt);
        if (j3 is not null)
            options.Add(ShippingCommerceRules.Apply(j3, query.ProductsTotalAfterDiscount, query.State, settings));

        if (settings.MelhorEnvioQuoteEnabled)
        {
            var meOptions = await TryQuoteMelhorEnvioAsync(query, settings, quotedAt, cancellationToken);
            foreach (var me in meOptions)
                options.Add(ShippingCommerceRules.Apply(me, query.ProductsTotalAfterDiscount, query.State, settings));
        }

        return options;
    }

    public async Task<NormalizedShippingOption> RequireOptionAsync(
        string shippingMethodId,
        ShippingQuoteQuery query,
        StoreSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!ShippingMethod.IsValid(shippingMethodId))
            throw new ValidationException("shippingMethodId", "Método de envio inválido.");

        var isMe = shippingMethodId is ShippingMethod.MelhorEconomico or ShippingMethod.MelhorExpresso;
        if (isMe && !settings.MelhorEnvioQuoteEnabled)
        {
            throw new ValidationException(
                "shippingMethodId",
                "Cotação Melhor Envio não está ativa.");
        }

        var options = await GetAvailableOptionsAsync(query, settings, cancellationToken);
        var selected = options.FirstOrDefault(o =>
            string.Equals(o.ShippingMethodId, shippingMethodId, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            throw new ValidationException(
                "shippingMethodId",
                "Modalidade de frete indisponível para este endereço. Recalcule o frete e tente novamente.");
        }

        return selected;
    }

    private NormalizedShippingOption? TryBuildJ3Option(
        ShippingQuoteQuery query,
        StoreSettings settings,
        DateTime quotedAtUtc)
    {
        var digits = query.DestinationCepDigits;
        if (digits.Length != 8 || !SimulatedShippingService.IsJ3CepEligible(digits))
            return null;

        var spNow = SimulatedShippingService.GetSaoPauloLocalTime(_clock.UtcNow);
        if (!J3WorkingDays.IsWorkingDay(spNow))
            return null;

        var cutoff = settings.J3CutoffHour is >= 0 and <= 23 ? settings.J3CutoffHour : 12;
        var days = spNow.Hour < cutoff ? 0 : 1;

        return new NormalizedShippingOption
        {
            ShippingMethodId = ShippingMethod.J3,
            Provider = ShippingMethod.GetProvider(ShippingMethod.J3),
            Name = "J3 Entregas",
            Description = days == 0
                ? "Pedido até o horário-limite: entrega no mesmo dia (dias úteis)."
                : "Pedido após o horário-limite: entrega no próximo dia útil.",
            CompanyId = null,
            ServiceId = null,
            CarrierName = "J3",
            ServiceName = "J3 Entregas",
            OriginalPrice = settings.J3Price,
            FinalPrice = settings.J3Price,
            EstimatedDaysMin = days,
            EstimatedDaysMax = days,
            FreeShippingApplied = false,
            SubsidyApplied = false,
            QuoteEnvironment = null,
            QuotedAtUtc = quotedAtUtc
        };
    }

    private async Task<IReadOnlyList<NormalizedShippingOption>> TryQuoteMelhorEnvioAsync(
        ShippingQuoteQuery query,
        StoreSettings settings,
        DateTime quotedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var originDigits = BrazilianCep.TryNormalize(settings.ShippingOriginCep) ?? "08061420";
            var weightGrams = settings.PackageWeightGrams > 0 ? settings.PackageWeightGrams : 400;
            var weightKg = weightGrams / 1000m;

            var calcRequest = new MelhorEnvioCalculateRequest(
                FromPostalCode: BrazilianCep.FormatMasked(originDigits),
                ToPostalCode: BrazilianCep.FormatMasked(query.DestinationCepDigits),
                HeightCm: settings.PackageHeightCm > 0 ? settings.PackageHeightCm : 6m,
                WidthCm: settings.PackageWidthCm > 0 ? settings.PackageWidthCm : 11m,
                LengthCm: settings.PackageLengthCm > 0 ? settings.PackageLengthCm : 16m,
                WeightKg: weightKg,
                Services: MelhorEnvioQuoteMapper.ServicesQuery);

            var environment = _meOptions.IsSandbox ? "sandbox" : (_meOptions.Environment ?? "sandbox");

            MelhorEnvioCalculateOutcome outcome;
            try
            {
                outcome = await _oauth.ExecuteWithTokenRetryAsync(
                    (token, ct) => _shipmentClient.CalculateAsync(calcRequest, token, ct),
                    r => r.Unauthenticated,
                    cancellationToken);
            }
            catch (MelhorEnvioOAuthException ex)
            {
                _logger.LogInformation(
                    "Melhor Envio cotação: OAuth indisponível ({Reason})",
                    ex.ReasonCode);
                return Array.Empty<NormalizedShippingOption>();
            }

            if (!outcome.Ok)
            {
                _logger.LogInformation(
                    "Melhor Envio cotação indisponível (timeout={Timeout}, rede={Network})",
                    outcome.TimedOut,
                    outcome.NetworkError);
                return Array.Empty<NormalizedShippingOption>();
            }

            var mapped = new List<NormalizedShippingOption>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in outcome.Services)
            {
                var option = MelhorEnvioQuoteMapper.TryMapService(raw, quotedAtUtc, environment);
                if (option is null)
                    continue;
                if (!seen.Add(option.ShippingMethodId))
                    continue;
                mapped.Add(option);
            }

            return mapped;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Melhor Envio cotação: falha inesperada (sem fallback simulado)");
            return Array.Empty<NormalizedShippingOption>();
        }
    }
}
