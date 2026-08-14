using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/j3-fulfillments")]
[Authorize(Roles = "Admin")]
public class AdminJ3FulfillmentsController : ControllerBase
{
    private readonly IJ3FulfillmentAdminQueryService _queries;

    public AdminJ3FulfillmentsController(IJ3FulfillmentAdminQueryService queries)
    {
        _queries = queries;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<J3FulfillmentAdminListItemDto>>> List(
        [FromQuery] J3FulfillmentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _queries.ListAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<J3FulfillmentAdminDetailDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _queries.GetAsync(id, cancellationToken);
        if (item is null)
            return NotFound();
        return Ok(item);
    }
}
