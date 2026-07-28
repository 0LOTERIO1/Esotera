namespace Esotera.Application.DTOs.Auth;

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(
    string Token,
    string NewPassword,
    string ConfirmPassword
);

public record PasswordMessageResponse(string Message);
