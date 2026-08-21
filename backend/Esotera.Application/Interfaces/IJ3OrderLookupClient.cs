using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Lookup read-only de pedido J3 por código Seller.
/// Zero createTmsOrders / importOrderByAccessKey.
/// </summary>
public interface IJ3OrderLookupClient
{
    Task<J3OrderLookupResult> SearchByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default);
}
