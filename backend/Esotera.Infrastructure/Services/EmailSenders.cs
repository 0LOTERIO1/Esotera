using System.Net;
using System.Net.Mail;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Envia e-mail via SMTP quando Email:Enabled e credenciais estão configuradas.
/// Não registra corpo completo nem senhas; apenas destino e assunto.
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

        var timeoutSeconds = Math.Clamp(_options.SmtpTimeoutSeconds, 3, 60);
        var timeoutMs = timeoutSeconds * 1000;

        using var client = new SmtpClient(_options.SmtpHost!, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseSsl,
            Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword),
            // System.Net.Mail.SmtpClient: Timeout padrão é 100_000 ms (~100s) — causa hang no Render.
            Timeout = timeoutMs
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        _logger.LogInformation(
            "SMTP iniciando envio. To={To} Subject={Subject} Host={Host} Port={Port} EnableSsl={EnableSsl} TimeoutSeconds={TimeoutSeconds}",
            message.To,
            message.Subject,
            _options.SmtpHost,
            _options.SmtpPort,
            _options.SmtpUseSsl,
            timeoutSeconds);

        try
        {
            await client.SendMailAsync(mail, timeoutCts.Token);
            _logger.LogInformation(
                "SMTP envio concluído. To={To} Subject={Subject}",
                message.To,
                message.Subject);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "SMTP timeout após {TimeoutSeconds}s. To={To} Subject={Subject} Host={Host} Port={Port}",
                timeoutSeconds,
                message.To,
                message.Subject,
                _options.SmtpHost,
                _options.SmtpPort);
            throw new TimeoutException(
                $"Timeout ao enviar e-mail via SMTP após {timeoutSeconds}s.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "SMTP falha no envio. To={To} Subject={Subject} Host={Host} Port={Port} ExceptionType={ExceptionType}",
                message.To,
                message.Subject,
                _options.SmtpHost,
                _options.SmtpPort,
                ex.GetType().Name);
            throw;
        }
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
            "E-mail NÃO enviado (SMTP não configurado). Destinatário: {To}. Assunto: {Subject}. Defina EMAIL_ENABLED=true e EMAIL_SMTP_HOST/USER/PASSWORD (ou Email__*).",
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

    /// <summary>Quando true, o próximo SendAsync lança TimeoutException (para testes).</summary>
    public bool FailNextWithTimeout { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (FailNextWithTimeout)
        {
            FailNextWithTimeout = false;
            throw new TimeoutException("SMTP timeout simulado.");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
