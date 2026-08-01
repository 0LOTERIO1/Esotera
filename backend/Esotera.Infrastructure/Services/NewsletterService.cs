using Esotera.Application.DTOs.Newsletter;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public class NewsletterService : INewsletterService
{
    private readonly EsoteraDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<NewsletterService> _logger;

    public NewsletterService(
        EsoteraDbContext context,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<NewsletterService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<NewsletterMessageResponse> SubscribeAsync(
        SubscribeNewsletterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Consent)
            throw new ValidationException("consent", "É necessário consentir em receber comunicações.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ValidationException("email", "Informe um e-mail válido.");

        var existing = await _context.NewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.Email == email, cancellationToken);

        var now = DateTime.UtcNow;
        string plainToken;
        bool reactivated;

        if (existing != null)
        {
            if (existing.IsActive)
                throw new ConflictException("Este e-mail já está inscrito na newsletter.");

            existing.IsActive = true;
            existing.ConsentAtUtc = now;
            existing.UnsubscribedAtUtc = null;
            existing.UpdatedAtUtc = now;
            plainToken = SecureToken.GenerateUrlSafeToken();
            existing.UnsubscribeTokenHash = SecureToken.Sha256Hex(plainToken);
            await _context.SaveChangesAsync(cancellationToken);
            reactivated = true;
        }
        else
        {
            plainToken = SecureToken.GenerateUrlSafeToken();
            _context.NewsletterSubscriptions.Add(new NewsletterSubscription
            {
                Id = Guid.NewGuid(),
                Email = email,
                IsActive = true,
                ConsentAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                UnsubscribeTokenHash = SecureToken.Sha256Hex(plainToken)
            });
            await _context.SaveChangesAsync(cancellationToken);
            reactivated = false;
        }

        // Persistência já concluída — e-mail nunca pode impedir a resposta HTTP.
        var confirmationSent = await TrySendSubscriptionEmailsAsync(
            email,
            plainToken,
            reactivated,
            cancellationToken);

        if (!_emailSender.IsConfigured)
        {
            return new NewsletterMessageResponse(
                reactivated
                    ? "Inscrição reativada com sucesso. Obrigado!"
                    : "Inscrição realizada com sucesso. Obrigado!",
                EmailSent: false);
        }

        if (confirmationSent)
        {
            return new NewsletterMessageResponse(
                reactivated
                    ? "Inscrição reativada. Enviamos um e-mail de confirmação."
                    : "Inscrição realizada. Enviamos um e-mail de confirmação.",
                EmailSent: true);
        }

        return new NewsletterMessageResponse(
            reactivated
                ? "Inscrição reativada com sucesso, mas não foi possível enviar o e-mail de confirmação. Tente novamente mais tarde."
                : "Inscrição realizada com sucesso, mas não foi possível enviar o e-mail de confirmação. Tente novamente mais tarde.",
            EmailSent: false);
    }

    public async Task<NewsletterMessageResponse> UnsubscribeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("token", "Token inválido.");

        var hash = SecureToken.Sha256Hex(token.Trim());
        var sub = await _context.NewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.UnsubscribeTokenHash == hash, cancellationToken);

        if (sub == null)
            throw new NotFoundException("Inscrição", token);

        if (!sub.IsActive)
            return new NewsletterMessageResponse("Esta inscrição já estava inativa.");

        var now = DateTime.UtcNow;
        sub.IsActive = false;
        sub.UnsubscribedAtUtc = now;
        sub.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new NewsletterMessageResponse("Você foi descadastrado da newsletter.");
    }

    public async Task<NewsletterAdminListResponse> AdminListAsync(
        string? search,
        bool? isActive,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);
        skip = Math.Max(0, skip);

        var query = _context.NewsletterSubscriptions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email.Contains(s));
        }

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(x => new NewsletterSubscriptionDto(
                x.Id,
                x.Email,
                x.IsActive,
                x.ConsentAtUtc,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.UnsubscribedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new NewsletterAdminListResponse(items, total);
    }

    public async Task<string> AdminExportCsvAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var list = await AdminListAsync(search, isActive, 0, 500, cancellationToken);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Email,Ativo,ConsentimentoUtc,CriadoUtc,AtualizadoUtc,DescadastradoUtc");
        foreach (var i in list.Items)
        {
            sb.Append(Escape(i.Email)).Append(',')
                .Append(i.IsActive ? "sim" : "nao").Append(',')
                .Append(i.ConsentAtUtc.ToString("o")).Append(',')
                .Append(i.CreatedAtUtc.ToString("o")).Append(',')
                .Append(i.UpdatedAtUtc.ToString("o")).Append(',')
                .Append(i.UnsubscribedAtUtc?.ToString("o") ?? "")
                .AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Envia confirmação e, em seguida, aviso ao admin (sequencial).
    /// Retorna true se a confirmação ao inscrito foi enviada.
    /// Nunca propaga exceção — inscrição já está salva.
    /// </summary>
    private async Task<bool> TrySendSubscriptionEmailsAsync(
        string email,
        string plainToken,
        bool reactivated,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_emailOptions.FrontendBaseUrl ?? "http://localhost:3000").TrimEnd('/');
        var unsubUrl = $"{baseUrl}/newsletter/descadastrar?token={Uri.EscapeDataString(plainToken)}";

        var subject = reactivated
            ? "Inscrição reativada — Newsletter Esotera"
            : "Confirmação de inscrição — Newsletter Esotera";

        var html = $"""
            <p>Olá,</p>
            <p>Sua inscrição na newsletter da Esotera foi {(reactivated ? "reativada" : "confirmada")}.</p>
            <p>Você receberá novidades e lançamentos por este e-mail.</p>
            <p><a href="{unsubUrl}">Descadastrar-se</a></p>
            <p>Esotera</p>
            """;

        var confirmationSent = false;
        try
        {
            _logger.LogInformation(
                "Newsletter: iniciando e-mail de confirmação. SmtpConfigured={Configured}",
                _emailSender.IsConfigured);
            await _emailSender.SendAsync(
                new EmailMessage(
                    email,
                    subject,
                    html,
                    $"Inscrição na newsletter Esotera confirmada. Para sair: {unsubUrl}"),
                cancellationToken);
            confirmationSent = _emailSender.IsConfigured;
            _logger.LogInformation(
                "Newsletter: confirmação processada. EmailSent={EmailSent}",
                confirmationSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Newsletter: falha ao enviar confirmação (inscrição já salva). ExceptionType={ExceptionType}",
                ex.GetType().Name);
        }

        var admin = _emailOptions.AdminNotifyEmail?.Trim();
        if (string.IsNullOrWhiteSpace(admin) || !_emailSender.IsConfigured)
            return confirmationSent;

        try
        {
            _logger.LogInformation("Newsletter: iniciando aviso ao administrador.");
            await _emailSender.SendAsync(
                new EmailMessage(
                    admin,
                    $"Nova inscrição newsletter — {email}",
                    $"<p>Novo e-mail inscrito na newsletter: <strong>{System.Net.WebUtility.HtmlEncode(email)}</strong></p>" +
                    $"<p>{(reactivated ? "Reativação" : "Nova inscrição")}.</p>",
                    $"Nova inscrição newsletter: {email}"),
                cancellationToken);
            _logger.LogInformation("Newsletter: aviso ao administrador processado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Newsletter: falha ao notificar admin (inscrição já salva). ExceptionType={ExceptionType}",
                ex.GetType().Name);
        }

        return confirmationSent;
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
