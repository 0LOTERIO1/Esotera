using Esotera.Application.DTOs.Auth;

namespace Esotera.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserDto> GetMeAsync(Guid userId);
    Task<PasswordMessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<PasswordMessageResponse> ResetPasswordAsync(ResetPasswordRequest request);
}
