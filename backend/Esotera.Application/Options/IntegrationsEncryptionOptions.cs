namespace Esotera.Application.Options;

/// <summary>
/// Chave AES-256 (32 bytes em Base64) para cifrar segredos de integrações.
/// Rotação exige reautorização das conexões (tokens não podem ser re-decifrados).
/// Variável: INTEGRATIONS_ENCRYPTION_KEY
/// </summary>
public class IntegrationsEncryptionOptions
{
    public const string SectionName = "IntegrationsEncryption";

    /// <summary>32 bytes codificados em Base64.</summary>
    public string? KeyBase64 { get; set; }

    public bool IsConfigured
    {
        get
        {
            if (string.IsNullOrWhiteSpace(KeyBase64))
                return false;
            try
            {
                var key = Convert.FromBase64String(KeyBase64.Trim());
                return key.Length == 32;
            }
            catch
            {
                return false;
            }
        }
    }
}
