using Esotera.Application.DTOs.Coupons;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _couponService;
    private readonly ICurrentUserService _currentUser;

    public CouponsController(ICouponService couponService, ICurrentUserService currentUser)
    {
        _couponService = couponService;
        _currentUser = currentUser;
    }

    [HttpPost("validate")]
    public async Task<ActionResult<CouponValidationResponse>> Validate([FromBody] CouponValidationRequest request)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var result = await _couponService.ValidateAsync(_currentUser.UserId.Value, request.Code, request.Subtotal);
        return Ok(result);
    }
}
