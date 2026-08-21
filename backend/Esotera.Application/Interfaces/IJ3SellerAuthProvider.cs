namespace Esotera.Application.Interfaces;

/// <summary>
/// Obtém accessToken Seller J3 via login oficial (portal). Cache em memória.
/// Nunca persiste token no DB. Nunca loga password/token.
/// </summary>
public interface IJ3SellerAuthProvider
{
    /// <summary>Token Bearer válido (cache ou novo login). Fail-closed se sellerId divergir.</summary>
    Task<J3SellerAuthResult> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Invalida cache em memória (ex.: após UNAUTHENTICATED em query read-only).</summary>
    void InvalidateCachedToken();
}

public sealed class J3SellerAuthResult
{
    public bool IsSuccess { get; init; }
    public string? AccessToken { get; init; }
    public string? ErrorCode { get; init; }

    public static J3SellerAuthResult Success(string accessToken) =>
        new() { IsSuccess = true, AccessToken = accessToken };

    public static J3SellerAuthResult Fail(string errorCode) =>
        new() { IsSuccess = false, ErrorCode = errorCode };
}
