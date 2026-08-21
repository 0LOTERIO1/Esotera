namespace Esotera.Application.DTOs.Fiscal;

/// <summary>
/// Resultado do parser NF-e (nfeProc / portal fiscal). Campos diagnósticos não são persistidos na entidade.
/// </summary>
public sealed class FiscalInvoiceParseResult
{
    public required string ChNFe { get; init; }
    public string? Number { get; init; }
    public string? Series { get; init; }
    public string? Model { get; init; }
    public string? Environment { get; init; }
    public DateTime? IssuedAtUtc { get; init; }
    public DateTime? AuthorizedAtUtc { get; init; }
    public string? IssuerCnpj { get; init; }
    public string? IssuerCrt { get; init; }
    public string? RecipientDocument { get; init; }
    public string? RecipientName { get; init; }
    public string? ProtocolNumber { get; init; }
    public string? ProtocolStatusCode { get; init; }
    public string? ProtocolStatusMessage { get; init; }
    public decimal? InvoiceTotal { get; init; }
    public IReadOnlyList<FiscalInvoiceParsedItem> Items { get; init; } = Array.Empty<FiscalInvoiceParsedItem>();

    /// <summary>
    /// True somente com protocolo autorizado: cStat=100, chNFe, nProt, dhRecbto e xMotivo de autorização.
    /// </summary>
    public bool HasAuthorizationEvidence { get; init; }
}

/// <param name="Sku">cProd original do XML (não normalizado).</param>
/// <param name="ExternalOrderRef">xPed do UpSeller — NÃO é OrderNumber da Esotera.</param>
public sealed record FiscalInvoiceParsedItem(
    string Sku,
    decimal Quantity,
    decimal? UnitPrice = null,
    decimal? LineTotal = null,
    string? Ncm = null,
    string? Cfop = null,
    string? Unit = null,
    string? ExternalOrderRef = null);
