using Esotera.Application.DTOs.Payments;
using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IPaymentService _payments;
    private readonly ICurrentUserService _currentUser;

    public PaymentsController(IPaymentService payments, ICurrentUserService currentUser)
    {
        _payments = payments;
        _currentUser = currentUser;
    }

    /// <summary>Config pública (sem secrets) para o frontend saber se está em Test/Production.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<PaymentEnvironmentConfigDto> GetConfig() =>
        Ok(_payments.GetPublicConfig());

    /// <summary>
    /// Pix oficial de teste R$ 50,00 (sandbox). Não cria pedido comercial.
    /// Bloqueado em Production.
    /// </summary>
    [HttpPost("sandbox/pix-test")]
    [Authorize]
    public async Task<ActionResult<SandboxPixTestResponse>> CreateSandboxPixTest(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        if (!Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = ["Cabeçalho Idempotency-Key é obrigatório."]
            }));
        }

        var result = await _payments.CreateSandboxPixTestAsync(
            _currentUser.UserId.Value,
            keyValues.First()!.Trim(),
            cancellationToken);

        return Ok(result);
    }
}
