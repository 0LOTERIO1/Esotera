using Esotera.Application.DTOs.Fiscal;

namespace Esotera.Application.Interfaces;

public interface IFiscalInvoiceImportService
{
    /// <summary>
    /// Importa XML para o pedido. Idempotente para o mesmo Order+ChNFe/SHA.
    /// Não chama J3/UpSeller/SEFAZ.
    /// </summary>
    Task<FiscalInvoiceImportResultDto> ImportXmlAsync(
        Guid orderId,
        Stream xmlStream,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken = default);
}
