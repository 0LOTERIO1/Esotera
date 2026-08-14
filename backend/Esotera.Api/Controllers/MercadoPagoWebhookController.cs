using Esotera.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/webhooks/mercadopago")]
[AllowAnonymous]
public class MercadoPagoWebhookController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly ILogger<MercadoPagoWebhookController> _logger;

    public MercadoPagoWebhookController(
        IPaymentService payments,
        ILogger<MercadoPagoWebhookController> logger)
    {
        _payments = payments;
        _logger = logger;
    }

    /// <summary>
    /// Webhook Mercado Pago (Orders API). URL de cadastro:
    /// https://esotera-api.onrender.com/api/webhooks/mercadopago
    /// Evento: Order (Mercado Pago) — topic order.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
            rawBody = await reader.ReadToEndAsync(cancellationToken);

        var xSignature = Request.Headers["x-signature"].FirstOrDefault();
        var xRequestId = Request.Headers["x-request-id"].FirstOrDefault();
        var dataId = Request.Query["data.id"].FirstOrDefault()
            ?? Request.Query["id"].FirstOrDefault();

        try
        {
            await _payments.ProcessWebhookAsync(
                rawBody,
                xSignature,
                xRequestId,
                dataId,
                cancellationToken);
        }
        catch (Application.Exceptions.ForbiddenException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            // Semântica HTTP inalterada: 200 mesmo em falha (evita storm de retry do MP).
            // Transaction local J3: se EnsurePending falha, Order+histórico também rollback
            // — 200 aqui NÃO deixa payment_approved sem obrigação.
            _logger.LogError(ex, "Falha ao processar webhook Mercado Pago.");
        }

        return Ok(new { received = true });
    }

    [HttpGet]
    public IActionResult Ping() => Ok(new { ok = true });
}
