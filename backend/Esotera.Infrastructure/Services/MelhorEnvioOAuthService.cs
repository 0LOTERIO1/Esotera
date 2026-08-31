using System.Data;
using System.Security.Cryptography;
using Esotera.Application.DTOs.Integrations;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public sealed class MelhorEnvioOAuthService : IMelhorEnvioOAuthService
{
    /// <summary>Advisory lock key estável para refresh entre instâncias (PostgreSQL).</summary>
    private const long RefreshAdvisoryLockKey = 872_364_01;

    private readonly EsoteraDbContext _db;
    private readonly MelhorEnvioOptions _options;
    private readonly IIntegrationsEncryptionService _encryption;
    private readonly IMelhorEnvioOAuthClient _oauthClient;
    private readonly IClock _clock;
    private readonly ILogger<MelhorEnvioOAuthService> _logger;

    public MelhorEnvioOAuthService(
        EsoteraDbContext db,
        IOptions<MelhorEnvioOptions> options,
        IIntegrationsEncryptionService encryption,
        IMelhorEnvioOAuthClient oauthClient,
        IClock clock,
        ILogger<MelhorEnvioOAuthService> logger)
    {
        _db = db;
        _options = options.Value;
        _encryption = encryption;
        _oauthClient = oauthClient;
        _clock = clock;
        _logger = logger;
    }

    public async Task<MelhorEnvioAuthorizeResponse> CreateAuthorizationUrlAsync(
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureOAuthReady();

        var plainState = SecureToken.GenerateUrlSafeToken(32);
        var stateHash = SecureToken.Sha256Hex(plainState);
        var now = _clock.UtcNow;

        _db.MelhorEnvioOAuthStates.Add(new MelhorEnvioOAuthState
        {
            Id = Guid.NewGuid(),
            StateHash = stateHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(MelhorEnvioOptions.OAuthStateLifetimeMinutes),
            CreatedByAdminUserId = adminUserId
        });
        await _db.SaveChangesAsync(cancellationToken);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!.Trim(),
            ["redirect_uri"] = _options.RedirectUri!.Trim(),
            ["response_type"] = "code",
            ["state"] = plainState,
            ["scope"] = MelhorEnvioOptions.RequestedScopes
        };

        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var url = $"{_options.AuthorizeUrl}?{qs}";
        _logger.LogInformation("Melhor Envio OAuth: URL de autorização gerada para admin {AdminUserId}", adminUserId);
        return new MelhorEnvioAuthorizeResponse(url);
    }

    public async Task<string> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogInformation("Melhor Envio OAuth: autorização recusada/erro do provedor");
            return FrontendError(MelhorEnvioOAuthReasons.Denied);
        }

        if (!_options.IsOAuthConfigured || !_encryption.IsConfigured)
        {
            _logger.LogWarning("Melhor Envio OAuth: configuração incompleta no callback");
            return FrontendError(MelhorEnvioOAuthReasons.ConfigMissing);
        }

        if (string.IsNullOrWhiteSpace(state))
            return FrontendError(MelhorEnvioOAuthReasons.StateInvalid);

        if (string.IsNullOrWhiteSpace(code))
            return FrontendError(MelhorEnvioOAuthReasons.MissingCode);

        var stateHash = SecureToken.Sha256Hex(state.Trim());
        var now = _clock.UtcNow;

        IDbContextTransaction? tx = null;
        if (_db.Database.IsRelational())
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var oauthState = await _db.MelhorEnvioOAuthStates
                .FirstOrDefaultAsync(s => s.StateHash == stateHash, cancellationToken);

            if (oauthState is null)
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.StateInvalid);
            }

            if (oauthState.UsedAtUtc is not null)
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.AlreadyUsed);
            }

            if (oauthState.ExpiresAtUtc < now)
            {
                oauthState.UsedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.StateExpired);
            }

            oauthState.UsedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);

            MelhorEnvioTokenResponse tokens;
            try
            {
                tokens = await _oauthClient.ExchangeAuthorizationCodeAsync(code.Trim(), cancellationToken);
            }
            catch (MelhorEnvioOAuthException)
            {
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.ExchangeFailed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Melhor Envio OAuth: falha na troca de código");
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.ExchangeFailed);
            }

            string accessCipher;
            string refreshCipher;
            try
            {
                accessCipher = _encryption.Encrypt(tokens.AccessToken);
                refreshCipher = _encryption.Encrypt(tokens.RefreshToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Melhor Envio OAuth: falha ao cifrar tokens");
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return FrontendError(MelhorEnvioOAuthReasons.EncryptionFailed);
            }

            var connection = await _db.MelhorEnvioConnections
                .OrderBy(c => c.ConnectedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var accessExpires = now.AddSeconds(tokens.ExpiresInSeconds);
            var refreshExpires = now.AddDays(MelhorEnvioOptions.RefreshTokenLifetimeDays);

            if (connection is null)
            {
                connection = new MelhorEnvioConnection
                {
                    Id = Guid.NewGuid(),
                    ConnectedAtUtc = now
                };
                _db.MelhorEnvioConnections.Add(connection);
            }

            connection.AccessTokenCipher = accessCipher;
            connection.RefreshTokenCipher = refreshCipher;
            connection.AccessTokenExpiresAtUtc = accessExpires;
            connection.RefreshTokenExpiresAtUtc = refreshExpires;
            connection.UpdatedAtUtc = now;
            connection.Scopes = MelhorEnvioOptions.RequestedScopes;
            connection.Environment = _options.NormalizedEnvironment;
            if (connection.ConnectedAtUtc == default)
                connection.ConnectedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Melhor Envio OAuth: conexão persistida (environment={Environment})",
                _options.NormalizedEnvironment);
            return FrontendSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Melhor Envio OAuth: falha ao persistir conexão");
            try
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
            }
            catch { /* ignore */ }
            return FrontendError(MelhorEnvioOAuthReasons.PersistFailed);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task<MelhorEnvioStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configured = _options.IsOAuthConfigured && _encryption.IsConfigured;
        var connection = await _db.MelhorEnvioConnections
            .AsNoTracking()
            .OrderBy(c => c.ConnectedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return new MelhorEnvioStatusDto(
                Connected: false,
                Configured: configured,
                Environment: null,
                Scopes: null,
                AccessTokenExpiresAtUtc: null,
                RefreshTokenExpiresAtUtc: null,
                ConnectedAtUtc: null,
                AccessTokenValid: false,
                NeedsReauthorization: false);
        }

        var now = _clock.UtcNow;
        var refreshStillValid = connection.RefreshTokenExpiresAtUtc > now;
        var accessValid = connection.AccessTokenExpiresAtUtc > now;
        var environmentMatches = EnvironmentMatches(connection);

        // Refresh lazy se perto de expirar e ainda houver refresh válido.
        // Nunca renovar token de outro ambiente: o endpoint de token seria o errado.
        if (configured
            && environmentMatches
            && refreshStillValid
            && connection.AccessTokenExpiresAtUtc <= now.AddHours(MelhorEnvioOptions.RefreshSkewHours))
        {
            try
            {
                await RefreshConnectionLockedAsync(cancellationToken);
                connection = await _db.MelhorEnvioConnections
                    .AsNoTracking()
                    .OrderBy(c => c.ConnectedAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Melhor Envio OAuth: refresh lazy no status falhou");
            }
        }

        if (connection is null)
        {
            return new MelhorEnvioStatusDto(
                false, configured, null, null, null, null, null, false, false);
        }

        now = _clock.UtcNow;
        accessValid = connection.AccessTokenExpiresAtUtc > now;
        refreshStillValid = connection.RefreshTokenExpiresAtUtc > now;
        environmentMatches = EnvironmentMatches(connection);

        var missingScopes = MelhorEnvioOptions.RequestedScopeList
            .Where(s => !MelhorEnvioOptions.HasAllScopes(connection.Scopes, [s]))
            .ToArray();
        var scopeMismatch = missingScopes.Length > 0;

        return new MelhorEnvioStatusDto(
            Connected: true,
            Configured: configured,
            Environment: connection.Environment,
            Scopes: connection.Scopes,
            AccessTokenExpiresAtUtc: connection.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc: connection.RefreshTokenExpiresAtUtc,
            ConnectedAtUtc: connection.ConnectedAtUtc,
            AccessTokenValid: accessValid && environmentMatches,
            NeedsReauthorization: !refreshStillValid || !environmentMatches || scopeMismatch,
            EnvironmentMismatch: !environmentMatches,
            ScopeMismatch: scopeMismatch,
            RequestedScopes: MelhorEnvioOptions.RequestedScopes,
            MissingScopes: missingScopes);
    }

    /// <summary>
    /// Token de sandbox não vale em produção (e vice-versa). Conexões antigas sem
    /// ambiente carimbado são tratadas como sandbox, que era o único ambiente possível.
    /// </summary>
    private bool EnvironmentMatches(MelhorEnvioConnection connection)
    {
        var saved = string.IsNullOrWhiteSpace(connection.Environment)
            ? "sandbox"
            : connection.Environment.Trim();
        return string.Equals(saved, _options.NormalizedEnvironment, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsOAuthConfigured || !_encryption.IsConfigured)
            return null;

        var connection = await _db.MelhorEnvioConnections
            .AsNoTracking()
            .OrderBy(c => c.ConnectedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
            return null;

        if (!EnvironmentMatches(connection))
        {
            _logger.LogWarning(
                "Melhor Envio: conexão salva pertence a outro ambiente (salvo={Saved}, atual={Current}). Reautorize.",
                connection.Environment,
                _options.NormalizedEnvironment);
            return null;
        }

        var now = _clock.UtcNow;
        if (connection.RefreshTokenExpiresAtUtc <= now)
            return null;

        if (connection.AccessTokenExpiresAtUtc <= now.AddHours(MelhorEnvioOptions.RefreshSkewHours))
        {
            try
            {
                await RefreshConnectionLockedAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Melhor Envio OAuth: refresh antes do uso falhou");
                if (connection.AccessTokenExpiresAtUtc <= now)
                    return null;
            }

            connection = await _db.MelhorEnvioConnections
                .AsNoTracking()
                .OrderBy(c => c.ConnectedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (connection is null)
                return null;
        }

        try
        {
            return _encryption.Decrypt(connection.AccessTokenCipher);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Melhor Envio OAuth: falha ao decifrar (chave rotacionada?)");
            return null;
        }
    }

    /// <summary>
    /// Para futuras calls ME: em resposta Unauthenticated, faz refresh e executa a ação novamente 1x.
    /// </summary>
    public async Task<T> ExecuteWithTokenRetryAsync<T>(
        Func<string, CancellationToken, Task<T>> action,
        Func<T, bool> isUnauthenticated,
        CancellationToken cancellationToken = default)
    {
        var token = await GetValidAccessTokenAsync(cancellationToken)
            ?? throw new MelhorEnvioOAuthException("not_connected");

        var result = await action(token, cancellationToken);
        if (!isUnauthenticated(result))
            return result;

        await RefreshConnectionLockedAsync(cancellationToken);
        token = await GetValidAccessTokenAsync(cancellationToken)
            ?? throw new MelhorEnvioOAuthException("not_connected");
        return await action(token, cancellationToken);
    }

    private async Task RefreshConnectionLockedAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction? tx = null;
        if (_db.Database.IsRelational())
            tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            await TryAcquireAdvisoryLockAsync(cancellationToken);

            var connection = await _db.MelhorEnvioConnections
                .OrderBy(c => c.ConnectedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (connection is null)
                throw new MelhorEnvioOAuthException("not_connected");

            var now = _clock.UtcNow;

            // Outra instância pode ter renovado enquanto aguardávamos o lock.
            if (connection.AccessTokenExpiresAtUtc > now.AddHours(MelhorEnvioOptions.RefreshSkewHours))
            {
                if (tx is not null) await tx.CommitAsync(cancellationToken);
                return;
            }

            if (connection.RefreshTokenExpiresAtUtc <= now)
                throw new MelhorEnvioOAuthException("refresh_expired");

            string refreshPlain;
            try
            {
                refreshPlain = _encryption.Decrypt(connection.RefreshTokenCipher);
            }
            catch (CryptographicException)
            {
                throw new MelhorEnvioOAuthException("encryption_failed");
            }

            var tokens = await _oauthClient.RefreshAsync(refreshPlain, cancellationToken);
            var accessCipher = _encryption.Encrypt(tokens.AccessToken);
            var refreshCipher = _encryption.Encrypt(tokens.RefreshToken);

            connection.AccessTokenCipher = accessCipher;
            connection.RefreshTokenCipher = refreshCipher;
            connection.AccessTokenExpiresAtUtc = now.AddSeconds(tokens.ExpiresInSeconds);
            connection.RefreshTokenExpiresAtUtc = now.AddDays(MelhorEnvioOptions.RefreshTokenLifetimeDays);
            connection.UpdatedAtUtc = now;

            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Melhor Envio OAuth: tokens renovados");
        }
        catch
        {
            try
            {
                if (tx is not null) await tx.RollbackAsync(cancellationToken);
            }
            catch { /* ignore */ }
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private async Task TryAcquireAdvisoryLockAsync(CancellationToken cancellationToken)
    {
        // InMemory / provedores sem PostgreSQL: Serializable + leitura da linha bastam nos testes.
        if (!_db.Database.IsNpgsql())
            return;

        await _db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({RefreshAdvisoryLockKey})",
            cancellationToken);
    }

    private void EnsureOAuthReady()
    {
        if (!_options.IsOAuthConfigured || !_encryption.IsConfigured)
            throw new MelhorEnvioOAuthException(MelhorEnvioOAuthReasons.ConfigMissing);
    }

    private string FrontendSuccess()
    {
        var baseUrl = NormalizeFrontendBase(_options.FrontendBaseUrl);
        return $"{baseUrl}/admin/configuracoes?me=connected";
    }

    private string FrontendError(string reason)
    {
        var baseUrl = NormalizeFrontendBase(_options.FrontendBaseUrl);
        var safe = SanitizeReason(reason);
        return $"{baseUrl}/admin/configuracoes?me=error&reason={Uri.EscapeDataString(safe)}";
    }

    private static string NormalizeFrontendBase(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "https://esotera.vercel.app" : value.Trim();
        return raw.TrimEnd('/');
    }

    private static string SanitizeReason(string reason)
    {
        // Apenas códigos controlados — sem tokens, messages brutas ou segredos.
        return reason switch
        {
            MelhorEnvioOAuthReasons.StateInvalid => MelhorEnvioOAuthReasons.StateInvalid,
            MelhorEnvioOAuthReasons.StateExpired => MelhorEnvioOAuthReasons.StateExpired,
            MelhorEnvioOAuthReasons.AlreadyUsed => MelhorEnvioOAuthReasons.AlreadyUsed,
            MelhorEnvioOAuthReasons.Denied => MelhorEnvioOAuthReasons.Denied,
            MelhorEnvioOAuthReasons.MissingCode => MelhorEnvioOAuthReasons.MissingCode,
            MelhorEnvioOAuthReasons.ExchangeFailed => MelhorEnvioOAuthReasons.ExchangeFailed,
            MelhorEnvioOAuthReasons.ConfigMissing => MelhorEnvioOAuthReasons.ConfigMissing,
            MelhorEnvioOAuthReasons.EncryptionFailed => MelhorEnvioOAuthReasons.EncryptionFailed,
            MelhorEnvioOAuthReasons.PersistFailed => MelhorEnvioOAuthReasons.PersistFailed,
            _ => MelhorEnvioOAuthReasons.ExchangeFailed
        };
    }
}
