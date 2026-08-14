using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.J3;

namespace Esotera.Application.Interfaces;

/// <summary>Leitura admin de J3Fulfillment. Zero HTTP J3. Zero mutation.</summary>
public interface IJ3FulfillmentAdminQueryService
{
    Task<PagedResult<J3FulfillmentAdminListItemDto>> ListAsync(
        J3FulfillmentFilterRequest filter,
        CancellationToken cancellationToken = default);

    Task<J3FulfillmentAdminDetailDto?> GetAsync(
        Guid fulfillmentId,
        CancellationToken cancellationToken = default);
}
