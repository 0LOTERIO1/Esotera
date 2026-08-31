using Esotera.Application.Common;
using Esotera.Application.DTOs.Integrations;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Diagnóstico da integração Melhor Envio para o Admin.
/// A sonda de autenticação usa POST shipment/calculate (escopo shipping-calculate,
/// o mesmo usado pela loja) — operação de leitura, sem carrinho nem compra.
/// </summary>
public sealed class MelhorEnvioDiagnosticsService : IMelhorEnvioDiagnosticsService
{
    /// <summary>CEPs fixos só para a sonda — não dependem de StoreSettings.</summary>
    private const string ProbeFromCep = "08061420";
    private const string ProbeToCep = "01001000";

    private readonly EsoteraDbContext _db;
    private readonly IMelhorEnvioOAuthService _oauth;
    private readonly IMelhorEnvioShipmentClient _shipmentClient;
    private readonly IIntegrationsEncryptionService _encryption;
    private readonly MelhorEnvioOptions _options;
    private readonly ILogger<MelhorEnvioDiagnosticsService> _logger;

    public MelhorEnvioDiagnosticsService(
        EsoteraDbContext db,
        IMelhorEnvioOAuthService oauth,
        IMelhorEnvioShipmentClient shipmentClient,
        IIntegrationsEncryptionService encryption,
        IOptions<MelhorEnvioOptions> options,
        ILogger<MelhorEnvioDiagnosticsService> logger)
    {
        _db = db;
        _oauth = oauth;
        _shipmentClient = shipmentClient;
        _encryption = encryption;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MelhorEnvioDiagnosticsDto> GetAsync(
        bool probe,
        CancellationToken cancellationToken = default)
    {
        var status = await _oauth.GetStatusAsync(cancellationToken);

        var tokenPresent = await _db.MelhorEnvioConnections
            .AsNoTracking()
            .AnyAsync(c => c.AccessTokenCipher != null && c.AccessTokenCipher != "", cancellationToken);

        var configured = _options.IsOAuthConfigured && _encryption.IsConfigured;
        bool? canAuthenticate = null;
        string message;

        if (!_options.Enabled)
        {
            message = "Integração desativada (MELHOR_ENVIO_ENABLED).";
        }
        else if (!_options.HasValidBaseUrl)
        {
            message = "Base URL inválida. Revise MELHOR_ENVIO_BASE_URL ou MELHOR_ENVIO_ENVIRONMENT.";
        }
        else if (!configured)
        {
            message = _encryption.IsConfigured
                ? "Configuração incompleta: verifique client id/secret, redirect URI, user agent e frontend base URL."
                : "INTEGRATIONS_ENCRYPTION_KEY ausente ou inválida — tokens não podem ser cifrados.";
        }
        else if (!status.Connected)
        {
            message = "Configurado, porém sem conexão OAuth. Conecte pelo Admin.";
        }
        else if (status.EnvironmentMismatch)
        {
            message =
                $"A conexão salva é do ambiente '{status.Environment}', mas o configurado é " +
                $"'{_options.NormalizedEnvironment}'. Reautorize antes de usar.";
        }
        else if (status.NeedsReauthorization)
        {
            message = "Refresh token expirado. Reautorize a conexão.";
        }
        else
        {
            message = "Conectado.";
        }

        var canProbe = probe
            && configured
            && status.Connected
            && !status.EnvironmentMismatch
            && !status.NeedsReauthorization;

        if (probe && !canProbe)
        {
            message += " Sonda não executada.";
        }
        else if (canProbe)
        {
            var (ok, probeMessage) = await ProbeAsync(cancellationToken);
            canAuthenticate = ok;
            message = probeMessage;
        }

        return new MelhorEnvioDiagnosticsDto(
            ConfiguredEnvironment: _options.NormalizedEnvironment,
            BaseUrl: _options.ResolvedBaseUrl,
            Configured: configured,
            TokenPresent: tokenPresent,
            CanAuthenticate: canAuthenticate,
            Message: message,
            Connection: status);
    }

    private async Task<(bool Ok, string Message)> ProbeAsync(CancellationToken cancellationToken)
    {
        var request = new MelhorEnvioCalculateRequest(
            FromPostalCode: BrazilianCep.FormatMasked(ProbeFromCep),
            ToPostalCode: BrazilianCep.FormatMasked(ProbeToCep),
            HeightCm: 6m,
            WidthCm: 11m,
            LengthCm: 16m,
            WeightKg: 0.4m,
            Services: MelhorEnvioQuoteMapper.ServicesQuery);

        try
        {
            var outcome = await _oauth.ExecuteWithTokenRetryAsync(
                (token, ct) => _shipmentClient.CalculateAsync(request, token, ct),
                r => r.Unauthenticated,
                cancellationToken);

            if (outcome.Ok)
            {
                var usable = outcome.Services.Count(s => !s.HasError);
                return (true, $"Autenticado. Cotação de teste retornou {usable} serviço(s) utilizável(is).");
            }

            if (outcome.Unauthenticated)
                return (false, "Token recusado pela API (401). Reautorize a conexão.");
            if (outcome.TimedOut)
                return (false, "Tempo esgotado ao falar com a API do Melhor Envio.");
            if (outcome.NetworkError)
                return (false, "Falha de rede ao falar com a API do Melhor Envio.");

            return (false, "A API do Melhor Envio respondeu com erro na cotação de teste.");
        }
        catch (MelhorEnvioOAuthException ex)
        {
            _logger.LogWarning("Melhor Envio diagnóstico: OAuth indisponível ({Reason})", ex.ReasonCode);
            return (false, $"OAuth indisponível ({ex.ReasonCode}).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Nunca logar token/segredo — só o tipo da falha.
            _logger.LogWarning(ex, "Melhor Envio diagnóstico: falha inesperada na sonda");
            return (false, "Falha inesperada na cotação de teste.");
        }
    }
}
