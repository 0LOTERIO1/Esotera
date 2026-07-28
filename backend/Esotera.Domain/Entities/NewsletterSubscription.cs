namespace Esotera.Domain.Entities;

public class NewsletterSubscription
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime ConsentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? UnsubscribedAtUtc { get; set; }
    /// <summary>SHA-256 hex do token de descadastramento (o valor em claro só vai no e-mail/link).</summary>
    public string UnsubscribeTokenHash { get; set; } = string.Empty;
}
