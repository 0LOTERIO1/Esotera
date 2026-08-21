namespace Esotera.Application.DTOs.Admin;

/// <summary>Resumo fiscal no detalhe Admin. Sem XML e sem documento completo.</summary>
public record AdminOrderFiscalSummaryDto(
    string FiscalStatus,
    string? MaskedChNFe,
    string? InvoiceNumber,
    string? InvoiceSeries,
    DateTime? AuthorizedAtUtc
);
