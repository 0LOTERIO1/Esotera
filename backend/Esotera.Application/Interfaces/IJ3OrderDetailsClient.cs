using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Lookup read-only de detalhes do pedido J3 por orderId (UUID).
/// Zero createTmsOrders / importOrderByAccessKey / reconcile.
/// </summary>
public interface IJ3OrderDetailsClient
{
    Task<J3OrderDetailsLookupResult> GetByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken = default);
}
