using Esotera.Application.DTOs.Integrations;

namespace Esotera.Application.Interfaces;

public interface IMelhorEnvioOAuthService
{
    Task<MelhorEnvioAuthorizeResponse> CreateAuthorizationUrlAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processa callback OAuth. Retorna URL absoluta do frontend (nunca tokens).
    /// </summary>
    Task<string> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default);

    Task<MelhorEnvioStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém access token válido (refresh lazy + lock). Para uso futuro em cotação.
    /// </summary>
    Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default);
}
