using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente OAuth fake para testes — sem HTTP real e sem segredos reais.
/// </summary>
public sealed class FakeMelhorEnvioOAuthClient : IMelhorEnvioOAuthClient
{
    private int _seq;

    public bool FailNextExchange { get; set; }
    public bool FailNextRefresh { get; set; }
    public bool UnauthenticatedOnRefresh { get; set; }

    public List<string> ExchangedCodes { get; } = new();
    public List<string> RefreshedTokens { get; } = new();

    public string LastAccessToken { get; private set; } = "";
    public string LastRefreshToken { get; private set; } = "";

    public Task<MelhorEnvioTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ExchangedCodes.Add(code);

        if (FailNextExchange)
        {
            FailNextExchange = false;
            throw new MelhorEnvioOAuthException("exchange_http_error");
        }

        var n = ++_seq;
        LastAccessToken = $"fake-access-{n}";
        LastRefreshToken = $"fake-refresh-{n}";
        return Task.FromResult(new MelhorEnvioTokenResponse(
            LastAccessToken,
            LastRefreshToken,
            2592000,
            "Bearer"));
    }

    public Task<MelhorEnvioTokenResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        RefreshedTokens.Add(refreshToken);

        if (FailNextRefresh)
        {
            FailNextRefresh = false;
            throw new MelhorEnvioOAuthException("exchange_http_error");
        }

        if (UnauthenticatedOnRefresh)
        {
            UnauthenticatedOnRefresh = false;
            throw new MelhorEnvioOAuthException("unauthenticated");
        }

        var n = ++_seq;
        LastAccessToken = $"fake-access-{n}";
        LastRefreshToken = $"fake-refresh-{n}";
        return Task.FromResult(new MelhorEnvioTokenResponse(
            LastAccessToken,
            LastRefreshToken,
            2592000,
            "Bearer"));
    }
}
