using Esotera.Application.DTOs.Integrations;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

/// <summary>
/// OAuth Melhor Envio. Authorize retorna JSON (Bearer no header) — o frontend navega para authorizationUrl.
/// </summary>
[ApiController]
[Route("api/integrations/melhor-envio")]
public class MelhorEnvioOAuthController : ControllerBase
{
    private readonly IMelhorEnvioOAuthService _oauth;
    private readonly ICurrentUserService _currentUser;

    public MelhorEnvioOAuthController(
        IMelhorEnvioOAuthService oauth,
        ICurrentUserService currentUser)
    {
        _oauth = oauth;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Inicia OAuth. Não redireciona: JWT admin vai no Authorization Bearer (não na query).
    /// </summary>
    [HttpGet("authorize")]
    [HttpPost("authorize")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MelhorEnvioAuthorizeResponse>> Authorize(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid adminId)
            return Unauthorized();

        try
        {
            var result = await _oauth.CreateAuthorizationUrlAsync(adminId, cancellationToken);
            return Ok(result);
        }
        catch (MelhorEnvioOAuthException ex) when (ex.ReasonCode == MelhorEnvioOAuthReasons.ConfigMissing)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Configuração incompleta",
                Detail = "Melhor Envio OAuth não está configurado no servidor.",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var redirectUrl = await _oauth.HandleCallbackAsync(code, state, error, cancellationToken);
        return Redirect(redirectUrl);
    }
}
