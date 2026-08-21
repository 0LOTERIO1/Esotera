namespace Esotera.Application.DTOs.J3;

/// <summary>
/// Smoke test admin read-only de autenticação Seller J3.
/// Nunca inclui accessToken, password, JWT ou payload J3.
/// </summary>
public sealed class J3SellerAuthCheckResponse
{
    public bool Success { get; init; }
    public bool Authenticated { get; init; }
    public bool SellerValidated { get; init; }
    public string? AuthMode { get; init; }
    public string? ErrorCode { get; init; }
}
