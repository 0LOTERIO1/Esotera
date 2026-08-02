using Esotera.Application.DTOs.Integrations;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/integrations/melhor-envio")]
[Authorize(Roles = "Admin")]
public class AdminMelhorEnvioController : ControllerBase
{
    private readonly IMelhorEnvioOAuthService _oauth;

    public AdminMelhorEnvioController(IMelhorEnvioOAuthService oauth)
    {
        _oauth = oauth;
    }

    /// <summary>Status da conexão (sem tokens).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<MelhorEnvioStatusDto>> Status(CancellationToken cancellationToken) =>
        Ok(await _oauth.GetStatusAsync(cancellationToken));
}
