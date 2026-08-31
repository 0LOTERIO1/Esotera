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
    private readonly IMelhorEnvioDiagnosticsService _diagnostics;

    public AdminMelhorEnvioController(
        IMelhorEnvioOAuthService oauth,
        IMelhorEnvioDiagnosticsService diagnostics)
    {
        _oauth = oauth;
        _diagnostics = diagnostics;
    }

    /// <summary>Status da conexão (sem tokens).</summary>
    [HttpGet("status")]
    public async Task<ActionResult<MelhorEnvioStatusDto>> Status(CancellationToken cancellationToken) =>
        Ok(await _oauth.GetStatusAsync(cancellationToken));

    /// <summary>
    /// Diagnóstico de configuração (sem segredos). `probe=true` executa uma cotação
    /// de teste para validar o token — operação de leitura, não compra etiqueta.
    /// </summary>
    [HttpGet("diagnostics")]
    public async Task<ActionResult<MelhorEnvioDiagnosticsDto>> Diagnostics(
        [FromQuery] bool probe,
        CancellationToken cancellationToken) =>
        Ok(await _diagnostics.GetAsync(probe, cancellationToken));
}
