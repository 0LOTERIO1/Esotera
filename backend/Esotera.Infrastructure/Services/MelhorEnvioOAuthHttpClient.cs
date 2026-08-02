using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public sealed class MelhorEnvioOAuthHttpClient : IMelhorEnvioOAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly MelhorEnvioOptions _options;
    private readonly ILogger<MelhorEnvioOAuthHttpClient> _logger;

    public MelhorEnvioOAuthHttpClient(
        HttpClient http,
        IOptions<MelhorEnvioOptions> options,
        ILogger<MelhorEnvioOAuthHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public Task<MelhorEnvioTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        // client_id como string — a API aceita; evita perda de precisão se ID for grande.
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId!.Trim(),
            ["client_secret"] = _options.ClientSecret!.Trim(),
            ["redirect_uri"] = _options.RedirectUri!.Trim(),
            ["code"] = code
        };

        return PostTokenAsync(body, cancellationToken);
    }

    public Task<MelhorEnvioTokenResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId!.Trim(),
            ["client_secret"] = _options.ClientSecret!.Trim(),
            ["refresh_token"] = refreshToken
        };

        return PostTokenAsync(body, cancellationToken);
    }

    private async Task<MelhorEnvioTokenResponse> PostTokenAsync(
        Dictionary<string, string> body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, MelhorEnvioOptions.SandboxTokenUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent!.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao solicitar token Melhor Envio");
            throw new MelhorEnvioOAuthException("exchange_network_error");
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Nunca logar body (pode conter detalhes sensíveis) nem code/tokens.
            _logger.LogWarning(
                "Melhor Envio token endpoint retornou {StatusCode}",
                (int)response.StatusCode);
            throw new MelhorEnvioOAuthException("exchange_http_error");
        }

        MelhorEnvioTokenJson? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<MelhorEnvioTokenJson>(raw, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resposta de token Melhor Envio inválida");
            throw new MelhorEnvioOAuthException("exchange_parse_error");
        }

        if (parsed is null
            || string.IsNullOrWhiteSpace(parsed.AccessToken)
            || string.IsNullOrWhiteSpace(parsed.RefreshToken)
            || parsed.ExpiresIn <= 0)
        {
            _logger.LogWarning("Resposta de token Melhor Envio incompleta");
            throw new MelhorEnvioOAuthException("exchange_incomplete");
        }

        return new MelhorEnvioTokenResponse(
            parsed.AccessToken,
            parsed.RefreshToken,
            parsed.ExpiresIn,
            parsed.TokenType);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsOAuthConfigured)
            throw new MelhorEnvioOAuthException("config_missing");
    }

    private sealed class MelhorEnvioTokenJson
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}

public sealed class MelhorEnvioOAuthException : Exception
{
    public string ReasonCode { get; }

    public MelhorEnvioOAuthException(string reasonCode)
        : base(reasonCode)
    {
        ReasonCode = reasonCode;
    }
}
