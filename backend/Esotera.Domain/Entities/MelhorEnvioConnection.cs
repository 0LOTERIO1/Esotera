namespace Esotera.Domain.Entities;

/// <summary>
/// Conexão OAuth Melhor Envio da loja. Tokens somente cifrados (AES-256-GCM).
/// </summary>
public class MelhorEnvioConnection
{
    public Guid Id { get; set; }

    /// <summary>Ciphertext Base64 (nonce || ciphertext || tag).</summary>
    public string AccessTokenCipher { get; set; } = string.Empty;

    /// <summary>Ciphertext Base64 (nonce || ciphertext || tag).</summary>
    public string RefreshTokenCipher { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
    public DateTime ConnectedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Escopos autorizados (ex.: shipping-calculate).</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>sandbox ou production.</summary>
    public string Environment { get; set; } = "sandbox";
}
