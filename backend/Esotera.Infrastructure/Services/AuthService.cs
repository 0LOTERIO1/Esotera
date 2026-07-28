using Esotera.Application.DTOs.Auth;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly EsoteraDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        EsoteraDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (emailExists)
            throw new ConflictException("Email já cadastrado.");

        var normalizedCpf = request.Cpf?.Replace(".", "").Replace("-", "").Trim();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = request.Email.ToLower().Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Cpf = normalizedCpf,
            Phone = request.Phone?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Trim(),
            Role = UserRole.Customer,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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

    private static UserDto MapToUserDto(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Cpf,
        user.Phone,
        user.Role.ToString()
    );
}
