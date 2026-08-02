using System.Security.Cryptography;
using System.Text;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// AES-256-GCM para tokens de integração. Formato: Base64(nonce12 || ciphertext || tag16).
/// </summary>
public sealed class IntegrationsEncryptionService : IIntegrationsEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[]? _key;

    public IntegrationsEncryptionService(IOptions<IntegrationsEncryptionOptions> options)
    {
        var opts = options.Value;
        if (!opts.IsConfigured)
        {
            _key = null;
            return;
        }

        _key = Convert.FromBase64String(opts.KeyBase64!.Trim());
    }

    public bool IsConfigured => _key is { Length: 32 };

    public string Encrypt(string plaintext)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("INTEGRATIONS_ENCRYPTION_KEY não configurada.");

        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key!, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var packed = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, packed, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, packed, NonceSize + cipher.Length, TagSize);
        return Convert.ToBase64String(packed);
    }

    public string Decrypt(string cipherBase64)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("INTEGRATIONS_ENCRYPTION_KEY não configurada.");

        ArgumentException.ThrowIfNullOrEmpty(cipherBase64);

        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(cipherBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext inválido.", ex);
        }

        if (packed.Length < NonceSize + TagSize + 1)
            throw new CryptographicException("Ciphertext inválido.");

        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(packed.Length - TagSize, TagSize);
        var cipher = packed.AsSpan(NonceSize, packed.Length - NonceSize - TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key!, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
