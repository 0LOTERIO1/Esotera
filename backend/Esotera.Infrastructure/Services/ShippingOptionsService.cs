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
/// Cotação centralizada: J3 real (coverage via IJ3Client) + Melhor Envio sandbox.
/// Sem fallback fictício ME. Falha J3 omite só a opção j3; demais carriers seguem.
/// Gate: Enabled primeiro (sem chamada client); depois config válida; coverage real.
/// Preço = StandardPriceCents/100 — nunca preço legado de StoreSettings, nunca default 1299 implícito.
/// Prazo J3: null/null ("Prazo a confirmar") — sem cutoff simulado nem faixas CEP legadas.
/// </summary>
public sealed class ShippingOptionsService : IShippingOptionsService
{
    private readonly IMelhorEnvioOAuthService _oauth;
    private readonly IMelhorEnvioShipmentClient _shipmentClient;
    private readonly IJ3Client _j3Client;
    private readonly MelhorEnvioOptions _meOptions;
    private readonly J3ShippingOptions _j3Options;
    private readonly IClock _clock;
    private readonly ILogger<ShippingOptionsService> _logger;

    public ShippingOptionsService(
        IMelhorEnvioOAuthService oauth,
        IMelhorEnvioShipmentClient shipmentClient,
        IJ3Client j3Client,
        IOptions<MelhorEnvioOptions> meOptions,
        IOptions<J3ShippingOptions> j3Options,
        IClock clock,
        ILogger<ShippingOptionsService> logger)
    {
        _oauth = oauth;
        _shipmentClient = shipmentClient;
        _j3Client = j3Client;
        _meOptions = meOptions.Value;
        _j3Options = j3Options.Value;
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

        var j3 = await TryBuildJ3OptionAsync(query, quotedAt, cancellationToken);
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

    /// <summary>
    /// Provider J3 real: Enabled → config → CEP → IsServiceAreaAsync.
    /// Sem faixas CEP simuladas, preço legado de StoreSettings, cutoff ou calendário útil legado.
    /// </summary>
    private async Task<NormalizedShippingOption?> TryBuildJ3OptionAsync(
        ShippingQuoteQuery query,
        DateTime quotedAtUtc,
        CancellationToken cancellationToken)
    {
        // 1) Gate Enabled primeiro — nunca chamar client com flag off.
        if (!_j3Options.Enabled)
            return null;

        // 2) Config mínima para oferecer J3 real (sem simulação).
        if (!_j3Options.HasValidRealQuoteConfig)
        {
            _logger.LogWarning(
                "J3 quote omitted: configuration incomplete (Enabled=true but URL/token/companyGroup/price invalid)");
            return null;
        }

        // 3) CEP inválido → não chamar J3.
        var digits = BrazilianCep.TryNormalize(query.DestinationCepDigits);
        if (digits is null)
            return null;

        // 4) Coverage real — falha operacional omite J3 (não 500 no quote).
        bool inServiceArea;
        try
        {
            inServiceArea = await _j3Client.IsServiceAreaAsync(digits, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (J3ApiException ex)
        {
            _logger.LogWarning(
                "J3 quote omitted: coverage call failed (operation={Operation}, http={HttpStatus})",
                ex.OperationName,
                ex.HttpStatus);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "J3 quote omitted: unexpected coverage failure");
            return null;
        }

        if (!inServiceArea)
            return null;

        var price = _j3Options.StandardPriceReais;
        return new NormalizedShippingOption
        {
            ShippingMethodId = ShippingMethod.J3,
            Provider = ShippingMethod.GetProvider(ShippingMethod.J3),
            Name = "J3 Entregas",
            Description = "Prazo a confirmar",
            CompanyId = null,
            ServiceId = null,
            CarrierName = "J3",
            ServiceName = "J3 Entregas",
            OriginalPrice = price,
            FinalPrice = price,
            EstimatedDaysMin = null,
            EstimatedDaysMax = null,
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
