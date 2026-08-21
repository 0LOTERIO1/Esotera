using Esotera.Application.DTOs.Fiscal;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Parser XML seguro para NF-e (nfeProc, namespace portal fiscal).
/// Sem validação de assinatura X509 nesta versão.
/// </summary>
public interface IFiscalInvoiceXmlParser
{
    /// <summary>Máximo de bytes aceitos (também enforced no endpoint).</summary>
    int MaxXmlBytes { get; }

    FiscalInvoiceParseResult Parse(ReadOnlyMemory<byte> xmlUtf8);
}
