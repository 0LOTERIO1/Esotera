namespace Esotera.Application.DTOs.Auth;

public record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string? Cpf,
    string? Phone
);
