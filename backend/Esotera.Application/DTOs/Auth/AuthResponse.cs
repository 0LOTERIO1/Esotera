namespace Esotera.Application.DTOs.Auth;

public record AuthResponse(string Token, UserDto User);

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string? Cpf,
    string? Phone,
    string Role
);
