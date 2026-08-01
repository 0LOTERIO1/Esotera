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

    public async Task<NewsletterMessageResponse> SubscribeAsync(SubscribeNewsletterRequest request)
    {
        if (!request.Consent)
            throw new ValidationException("consent", "É necessário consentir em receber comunicações.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ValidationException("email", "Informe um e-mail válido.");

        var existing = await _context.NewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.Email == email);

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
            await _context.SaveChangesAsync();
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
            await _context.SaveChangesAsync();
            reactivated = false;
        }

        await TrySendSubscriptionEmailsAsync(email, plainToken, reactivated);

        if (_emailSender.IsConfigured)
        {
            return new NewsletterMessageResponse(
                reactivated
                    ? "Inscrição reativada. Enviamos um e-mail de confirmação."
                    : "Inscrição realizada. Enviamos um e-mail de confirmação.");
        }

        return new NewsletterMessageResponse(
            reactivated
                ? "Inscrição reativada com sucesso. Obrigado!"
                : "Inscrição realizada com sucesso. Obrigado!");
    }

    public async Task<NewsletterMessageResponse> UnsubscribeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("token", "Token inválido.");

        var hash = SecureToken.Sha256Hex(token.Trim());
        var sub = await _context.NewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.UnsubscribeTokenHash == hash);

        if (sub == null)
            throw new NotFoundException("Inscrição", token);

        if (!sub.IsActive)
            return new NewsletterMessageResponse("Esta inscrição já estava inativa.");

        var now = DateTime.UtcNow;
        sub.IsActive = false;
        sub.UnsubscribedAtUtc = now;
        sub.UpdatedAtUtc = now;
        await _context.SaveChangesAsync();

        return new NewsletterMessageResponse("Você foi descadastrado da newsletter.");
    }

    public async Task<NewsletterAdminListResponse> AdminListAsync(
        string? search,
        bool? isActive,
        int skip = 0,
        int take = 100)
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

        var total = await query.CountAsync();
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
            .ToArrayAsync();

        return new NewsletterAdminListResponse(items, total);
    }

    public async Task<string> AdminExportCsvAsync(string? search, bool? isActive)
    {
        var list = await AdminListAsync(search, isActive, 0, 500);
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

    private async Task TrySendSubscriptionEmailsAsync(string email, string plainToken, bool reactivated)
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

        try
        {
            await _emailSender.SendAsync(new EmailMessage(
                email,
                subject,
                html,
                $"Inscrição na newsletter Esotera confirmada. Para sair: {unsubUrl}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar confirmação de newsletter para inscrito.");
        }

        var admin = _emailOptions.AdminNotifyEmail?.Trim();
        if (string.IsNullOrWhiteSpace(admin))
            return;

        try
        {
            await _emailSender.SendAsync(new EmailMessage(
                admin,
                $"Nova inscrição newsletter — {email}",
                $"<p>Novo e-mail inscrito na newsletter: <strong>{System.Net.WebUtility.HtmlEncode(email)}</strong></p>" +
                $"<p>{(reactivated ? "Reativação" : "Nova inscrição")}.</p>",
                $"Nova inscrição newsletter: {email}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao notificar admin sobre inscrição na newsletter.");
        }
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
