namespace Esotera.Domain.Entities;

/// <summary>
/// NF-e importada (ex.: XML autorizado do UpSeller). Sem emissão SEFAZ na Esotera.
/// XML armazenado cifrado (XmlCipher). Sem certificado/A1/NCM/regras tributárias.
/// </summary>
public class FiscalInvoice
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }

    /// <summary><see cref="Enums.FiscalInvoiceStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Chave de acesso 44 dígitos. Unique quando preenchida.</summary>
    public string? ChNFe { get; set; }

    public string? Number { get; set; }
    public string? Series { get; set; }

    /// <summary>Ambiente fiscal (ex.: 1 produção / 2 homologação). Confirmado após XML real.</summary>
    public string? Environment { get; set; }

    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? AuthorizedAtUtc { get; set; }

    public string? IssuerCnpj { get; set; }

    /// <summary>CPF ou CNPJ do destinatário (somente dígitos). Não expor completo na UI de lista.</summary>
    public string? RecipientDocument { get; set; }

    public string? ProtocolNumber { get; set; }

    /// <summary>XML UTF-8 cifrado via IIntegrationsEncryptionService (AES-GCM Base64).</summary>
    public string XmlCipher { get; set; } = string.Empty;

    /// <summary>SHA-256 hex do XML em claro (idempotência / integridade). Sem PII além do hash.</summary>
    public string XmlSha256 { get; set; } = string.Empty;

    /// <summary><see cref="Enums.FiscalInvoiceSource"/>.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
