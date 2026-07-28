using System.Net;
using System.Net.Mail;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Envia e-mail via SMTP quando Email:Enabled e credenciais estão configuradas.
/// Não registra corpo completo nem senhas; apenas destino e assunto em nível Debug/Information.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsSmtpConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("SMTP de e-mail não está configurado.");

        using var client = new SmtpClient(_options.SmtpHost!, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseSsl,
            Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        if (!string.IsNullOrWhiteSpace(message.TextBody))
            mail.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

        _logger.LogInformation("Enviando e-mail para {To} com assunto {Subject}", message.To, message.Subject);
        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>
/// Usado quando SMTP não está configurado. Não envia; registra aviso sem dados sensíveis.
/// </summary>
public class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public bool IsConfigured => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "E-mail NÃO enviado (SMTP não configurado). Destinatário: {To}. Assunto: {Subject}. Configure Email__Enabled e SMTP_* no ambiente.",
            message.To,
            message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Captura e-mails em memória (testes).</summary>
public class CapturingEmailSender : IEmailSender
{
    public bool IsConfigured => true;
    public List<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
