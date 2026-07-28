namespace Esotera.Application.Interfaces;

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null
);

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
