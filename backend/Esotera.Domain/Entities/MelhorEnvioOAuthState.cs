namespace Esotera.Domain.Entities;

/// <summary>
/// State OAuth one-shot. Persiste apenas o hash SHA-256 do state (nunca o valor em claro).
/// </summary>
public class MelhorEnvioOAuthState
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 hex do state enviado ao Melhor Envio.</summary>
    public string StateHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedByAdminUserId { get; set; }
}
