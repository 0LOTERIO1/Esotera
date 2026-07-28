using Esotera.Application.DTOs.Coupons;
using Esotera.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/coupons")]
[Authorize(Roles = "Admin")]
public class AdminCouponsController : ControllerBase
{
    private readonly ICouponService _coupons;
    private readonly IValidator<CreateCouponRequest> _createValidator;
    private readonly IValidator<UpdateCouponRequest> _updateValidator;

    public AdminCouponsController(
        ICouponService coupons,
        IValidator<CreateCouponRequest> createValidator,
        IValidator<UpdateCouponRequest> updateValidator)
    {
        _coupons = coupons;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCouponDto>>> List(
        [FromQuery] bool? isArchived = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? archived = null)
    {
        bool? archivedFilter = isArchived;
        if (string.Equals(archived, "all", StringComparison.OrdinalIgnoreCase))
            archivedFilter = null;
        else if (string.Equals(archived, "only", StringComparison.OrdinalIgnoreCase))
            archivedFilter = true;
        else if (archivedFilter is null)
            archivedFilter = false;

        return Ok(await _coupons.AdminListAsync(archivedFilter, isActive));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCouponDto>> Get(Guid id)
    {
        var coupon = await _coupons.AdminGetAsync(id);
        return coupon == null ? NotFound() : Ok(coupon);
    }

    [HttpPost]
    public async Task<ActionResult<AdminCouponDto>> Create([FromBody] CreateCouponRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var created = await _coupons.AdminCreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminCouponDto>> Update(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        return Ok(await _coupons.AdminUpdateAsync(id, request));
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<ActionResult<AdminCouponDto>> Activate(Guid id) =>
        Ok(await _coupons.AdminSetActiveAsync(id, true));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<ActionResult<AdminCouponDto>> Deactivate(Guid id) =>
        Ok(await _coupons.AdminSetActiveAsync(id, false));

    [HttpPatch("{id:guid}/archive")]
    public async Task<ActionResult<AdminCouponDto>> Archive(Guid id) =>
        Ok(await _coupons.AdminArchiveAsync(id));

    [HttpPatch("{id:guid}/restore")]
    public async Task<ActionResult<AdminCouponDto>> Restore(Guid id) =>
        Ok(await _coupons.AdminRestoreAsync(id));
}
