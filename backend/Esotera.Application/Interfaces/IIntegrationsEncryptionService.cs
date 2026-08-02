namespace Esotera.Application.Interfaces;

public interface IIntegrationsEncryptionService
{
    bool IsConfigured { get; }

    /// <summary>Cifra plaintext → Base64 (nonce || ciphertext || tag).</summary>
    string Encrypt(string plaintext);

    /// <summary>Decifra Base64 AES-GCM. Falha se a chave foi rotacionada.</summary>
    string Decrypt(string cipherBase64);
}
