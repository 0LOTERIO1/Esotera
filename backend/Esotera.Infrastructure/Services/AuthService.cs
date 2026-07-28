using Esotera.Application;
using Esotera.Application.DTOs.Auth;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public class AuthService : IAuthService
{
    public const string GenericPasswordResetMessage =
        "Se o e-mail informado estiver cadastrado, enviaremos instruções para redefinir a senha.";

    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly EsoteraDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        EsoteraDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (!request.AcceptedTerms)
            throw new ValidationException("acceptedTerms", "Aceite os termos de uso para continuar.");
        if (!request.AcceptedPrivacy)
            throw new ValidationException("acceptedPrivacy", "Aceite a política de privacidade para continuar.");

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (emailExists)
            throw new ConflictException("Email já cadastrado.");

        var normalizedCpf = request.Cpf?.Replace(".", "").Replace("-", "").Trim();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.ToLower().Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Cpf = normalizedCpf,
            Phone = request.Phone?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Trim(),
            Role = UserRole.Customer,
            TermsAcceptedAtUtc = now,
            PrivacyAcceptedAtUtc = now,
            TermsVersion = LegalDocuments.TermsVersion,
            PrivacyVersion = LegalDocuments.PrivacyVersion,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, MapToUserDto(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAppException("Email ou senha inválidos.");

        var token = _jwtTokenService.GenerateToken(user);
        return new AuthResponse(token, MapToUserDto(user));
    }

    public async Task<UserDto> GetMeAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new NotFoundException("Usuário", userId);

        return MapToUserDto(user);
    }

    public async Task<PasswordMessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null)
        {
            var now = DateTime.UtcNow;
            var activeTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.UsedAtUtc == null && t.ExpiresAtUtc > now)
                .ToListAsync();

            foreach (var t in activeTokens)
                t.UsedAtUtc = now;

            var plainToken = SecureToken.GenerateUrlSafeToken();
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = SecureToken.Sha256Hex(plainToken),
                ExpiresAtUtc = now.Add(ResetTokenLifetime),
                CreatedAtUtc = now
            });
            await _context.SaveChangesAsync();

            var baseUrl = (_emailOptions.FrontendBaseUrl ?? "http://localhost:3000").TrimEnd('/');
            var resetUrl = $"{baseUrl}/redefinir-senha?token={Uri.EscapeDataString(plainToken)}";

            var html = $"""
                <p>Olá,</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta na Esotera.</p>
                <p><a href="{resetUrl}">Clique aqui para escolher uma nova senha</a></p>
                <p>Este link expira em cerca de 30 minutos e pode ser usado apenas uma vez.</p>
                <p>Se você não solicitou esta alteração, ignore este e-mail.</p>
                <p>Esotera · { _emailOptions.FromAddress }</p>
                """;

            try
            {
                await _emailSender.SendAsync(new EmailMessage(
                    user.Email,
                    "Redefinição de senha — Esotera",
                    html,
                    $"Acesse o link para redefinir sua senha (válido por 30 minutos): {resetUrl}"));

                if (!_emailSender.IsConfigured)
                {
                    _logger.LogWarning(
                        "Recuperação de senha gerada para usuário {UserId}, mas SMTP não está configurado — e-mail não foi entregue.",
                        user.Id);
                }
            }
            catch (Exception ex)
            {
                // Não revela falha de envio ao cliente; registra sem senha/token
                _logger.LogError(ex, "Falha ao enviar e-mail de recuperação para usuário {UserId}", user.Id);
            }
        }

        return new PasswordMessageResponse(GenericPasswordResetMessage);
    }

    public async Task<PasswordMessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("token", "Token inválido.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            throw new ValidationException("newPassword", "A senha deve ter ao menos 6 caracteres.");

        if (request.NewPassword != request.ConfirmPassword)
            throw new ValidationException("confirmPassword", "As senhas não coincidem.");

        var hash = SecureToken.Sha256Hex(request.Token.Trim());
        var now = DateTime.UtcNow;

        var reset = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (reset == null || reset.UsedAtUtc != null || reset.ExpiresAtUtc < now)
            throw new ValidationException("token", "Token inválido ou expirado.");

        reset.UsedAtUtc = now;
        reset.User.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        reset.User.UpdatedAtUtc = now;

        var siblings = await _context.PasswordResetTokens
            .Where(t => t.UserId == reset.UserId && t.Id != reset.Id && t.UsedAtUtc == null)
            .ToListAsync();
        foreach (var s in siblings)
            s.UsedAtUtc = now;

        await _context.SaveChangesAsync();

        return new PasswordMessageResponse("Senha redefinida com sucesso. Você já pode entrar com a nova senha.");
    }

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Cpf,
        user.Phone,
        user.Role.ToString()
    );
}
