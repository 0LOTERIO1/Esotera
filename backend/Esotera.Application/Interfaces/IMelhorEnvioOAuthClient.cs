namespace Esotera.Application.Interfaces;

public sealed record MelhorEnvioTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    string? TokenType);

public interface IMelhorEnvioOAuthClient
{
    Task<MelhorEnvioTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<MelhorEnvioTokenResponse> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}
