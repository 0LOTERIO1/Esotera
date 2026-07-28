using Esotera.Application.DTOs.Addresses;
using Esotera.Application.DTOs.Auth;
using Esotera.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAddressService _addressService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateAddressRequest> _addressValidator;

    public UsersController(
        IAuthService authService,
        IAddressService addressService,
        ICurrentUserService currentUser,
        IValidator<CreateAddressRequest> addressValidator)
    {
        _authService = authService;
        _addressService = addressService;
        _currentUser = currentUser;
        _addressValidator = addressValidator;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var user = await _authService.GetMeAsync(_currentUser.UserId.Value);
        return Ok(user);
    }

    [HttpGet("me/addresses")]
    public async Task<ActionResult<IReadOnlyList<AddressDto>>> ListAddresses()
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var addresses = await _addressService.ListForUserAsync(_currentUser.UserId.Value);
        return Ok(addresses);
    }

    [HttpGet("me/addresses/{id:guid}")]
    public async Task<ActionResult<AddressDto>> GetAddress(Guid id)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var address = await _addressService.GetByIdAsync(_currentUser.UserId.Value, id);
        if (address == null)
            return NotFound();

        return Ok(address);
    }

    [HttpPost("me/addresses")]
    public async Task<ActionResult<AddressDto>> CreateAddress([FromBody] CreateAddressRequest request)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var validation = await _addressValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var address = await _addressService.CreateAsync(_currentUser.UserId.Value, request);
        return CreatedAtAction(nameof(GetAddress), new { id = address.Id }, address);
    }

    [HttpPut("me/addresses/{id:guid}")]
    public async Task<ActionResult<AddressDto>> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var address = await _addressService.UpdateAsync(_currentUser.UserId.Value, id, request);
        return Ok(address);
    }

    [HttpDelete("me/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        await _addressService.DeleteAsync(_currentUser.UserId.Value, id);
        return NoContent();
    }

    [HttpPost("me/addresses/{id:guid}/set-primary")]
    public async Task<IActionResult> SetPrimaryAddress(Guid id)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        await _addressService.SetPrimaryAsync(_currentUser.UserId.Value, id);
        return NoContent();
    }
}
