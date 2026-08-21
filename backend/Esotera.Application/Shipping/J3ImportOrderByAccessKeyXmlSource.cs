using System.Text;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Interfaces;

namespace Esotera.Application.Shipping;

/// <summary>
/// Decripta XmlCipher server-side e parseia — nunca envia XML/cipher à J3.
/// </summary>
public static class J3ImportOrderByAccessKeyXmlSource
{
    public static FiscalInvoiceParseResult ParseFromCipher(
        string xmlCipher,
        IIntegrationsEncryptionService encryption,
        IFiscalInvoiceXmlParser parser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlCipher);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(parser);

        if (!encryption.IsConfigured)
            throw new InvalidOperationException("Integrations encryption is not configured.");

        var plain = encryption.Decrypt(xmlCipher);
        return parser.Parse(Encoding.UTF8.GetBytes(plain));
    }
}
