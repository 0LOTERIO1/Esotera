namespace Esotera.Application.DTOs.Integrations;

public sealed record MelhorEnvioAuthorizeResponse(string AuthorizationUrl);

public sealed record MelhorEnvioStatusDto(
    bool Connected,
    bool Configured,
    string? Environment,
    string? Scopes,
    DateTime? AccessTokenExpiresAtUtc,
    DateTime? RefreshTokenExpiresAtUtc,
    DateTime? ConnectedAtUtc,
    bool AccessTokenValid,
    bool NeedsReauthorization);

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
