namespace Esotera.Application.DTOs.Fiscal;

public record FiscalInvoiceImportResultDto(
    string Status,
    string MaskedChNFe,
    string? Number,
    string? Series,
    DateTime? AuthorizedAtUtc,
    bool IdempotentReplay
);
