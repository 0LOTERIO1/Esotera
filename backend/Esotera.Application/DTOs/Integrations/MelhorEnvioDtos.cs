namespace Esotera.Application.DTOs.Integrations;

public sealed record MelhorEnvioAuthorizeResponse(string AuthorizationUrl);

/// <summary>
/// Status da conexão. NUNCA inclui access token, refresh token ou client secret.
/// </summary>
public sealed record MelhorEnvioStatusDto(
    bool Connected,
    bool Configured,
    string? Environment,
    string? Scopes,
    DateTime? AccessTokenExpiresAtUtc,
    DateTime? RefreshTokenExpiresAtUtc,
    DateTime? ConnectedAtUtc,
    bool AccessTokenValid,
    bool NeedsReauthorization,
    /// <summary>Conexão salva é de outro ambiente que o configurado — exige reautorizar.</summary>
    bool EnvironmentMismatch = false);

/// <summary>
/// Diagnóstico Admin-only. Sem segredos: expõe apenas presença/validade.
/// </summary>
public sealed record MelhorEnvioDiagnosticsDto(
    string ConfiguredEnvironment,
    string BaseUrl,
    bool Configured,
    bool TokenPresent,
    /// <summary>null quando a sonda não foi executada (probe=false).</summary>
    bool? CanAuthenticate,
    string Message,
    MelhorEnvioStatusDto Connection);

public static class MelhorEnvioOAuthReasons
{
    public const string StateInvalid = "state_invalid";
    public const string StateExpired = "state_expired";
    public const string AlreadyUsed = "already_used";
    public const string Denied = "denied";
    public const string MissingCode = "missing_code";
    public const string ExchangeFailed = "exchange_failed";
    public const string ConfigMissing = "config_missing";
    public const string EncryptionFailed = "encryption_failed";
    public const string PersistFailed = "persist_failed";
}
