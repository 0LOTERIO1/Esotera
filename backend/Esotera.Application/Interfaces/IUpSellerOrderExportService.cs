namespace Esotera.Application.Interfaces;

public sealed record UpSellerExportFile(
    byte[] Content,
    string FileName,
    string ContentType
);

/// <summary>Gera .xlsx no layout oficial UpSeller (aba order_) a partir de um pedido pago.</summary>
public interface IUpSellerOrderExportService
{
    Task<UpSellerExportFile> ExportOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
