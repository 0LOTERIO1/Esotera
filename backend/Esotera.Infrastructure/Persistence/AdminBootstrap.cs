using Esotera.Application;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esotera.Infrastructure.Persistence;

/// <summary>
/// Cria o primeiro administrador real a partir de variáveis de ambiente (ex.: Render).
/// Não altera usuários existentes e nunca registra senha/hash nos logs.
/// </summary>
public class AdminBootstrap
{
    private readonly EsoteraDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrap> _logger;

    public AdminBootstrap(
        EsoteraDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<AdminBootstrap> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var enabled = _configuration.GetValue("BOOTSTRAP_ADMIN_ENABLED", false);
        if (!enabled)
            return;

        var name = _configuration["BOOTSTRAP_ADMIN_NAME"]?.Trim();
        var emailRaw = _configuration["BOOTSTRAP_ADMIN_EMAIL"]?.Trim();
        var password = _configuration["BOOTSTRAP_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(emailRaw)
            || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "Bootstrap admin habilitado, mas BOOTSTRAP_ADMIN_NAME/EMAIL/PASSWORD estão incompletos. Nenhuma conta criada.");
            return;
        }

        if (password.Length < 6)
        {
            _logger.LogWarning(
                "Bootstrap admin: senha não atende ao mínimo de 6 caracteres. Nenhuma conta criada.");
            return;
        }

        if (name.Length > 200 || emailRaw.Length > 256)
        {
            _logger.LogWarning(
                "Bootstrap admin: nome ou e-mail excedem o tamanho máximo. Nenhuma conta criada.");
            return;
        }

        if (!emailRaw.Contains('@', StringComparison.Ordinal))
        {
            _logger.LogWarning("Bootstrap admin: e-mail inválido. Nenhuma conta criada.");
            return;
        }

        var email = emailRaw.ToLowerInvariant();

        var exists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email, cancellationToken);

        if (exists)
        {
            _logger.LogInformation(
                "Bootstrap admin: já existe usuário com o e-mail informado. Nenhuma alteração.");
            return;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            Role = UserRole.Admin,
            TermsAcceptedAtUtc = now,
            PrivacyAcceptedAtUtc = now,
            TermsVersion = LegalDocuments.TermsVersion,
            PrivacyVersion = LegalDocuments.PrivacyVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Bootstrap admin: administrador criado com role {Role} (id={UserId}). Desative BOOTSTRAP_ADMIN_ENABLED e remova BOOTSTRAP_ADMIN_PASSWORD.",
            UserRole.Admin.ToString(),
            user.Id);
    }
}
