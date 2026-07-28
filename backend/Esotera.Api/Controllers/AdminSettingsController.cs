using Esotera.Application.DTOs.Settings;
using Esotera.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IStoreSettingsService _settings;

    public SettingsController(IStoreSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicStoreSettingsDto>> GetPublic() =>
        Ok(await _settings.GetPublicAsync());
}

[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "Admin")]
public class AdminSettingsController : ControllerBase
{
    private readonly IStoreSettingsService _settings;
    private readonly IValidator<UpdateStoreSettingsRequest> _validator;

    public AdminSettingsController(
        IStoreSettingsService settings,
        IValidator<UpdateStoreSettingsRequest> validator)
    {
        _settings = settings;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<AdminStoreSettingsDto>> Get() =>
        Ok(await _settings.GetAdminAsync());

    [HttpPut]
    public async Task<ActionResult<AdminStoreSettingsDto>> Update([FromBody] UpdateStoreSettingsRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        return Ok(await _settings.UpdateAsync(request));
    }
}
