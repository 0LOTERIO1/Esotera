using Esotera.Application.DTOs.Payments;
using Esotera.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderPaymentsController : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IPaymentService _payments;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreatePaymentRequest> _validator;

    public OrderPaymentsController(
        IPaymentService payments,
        ICurrentUserService currentUser,
        IValidator<CreatePaymentRequest> validator)
    {
        _payments = payments;
        _currentUser = currentUser;
        _validator = validator;
    }

    /// <summary>
    /// Cria pagamento no Mercado Pago para um pedido awaiting_payment.
    /// Body nunca deve conter número de cartão ou CVV — apenas token do Brick.
    /// </summary>
    [HttpPost("{orderId:guid}/payments")]
    public async Task<ActionResult<CreatePaymentResponse>> CreatePayment(
        Guid orderId,
        [FromBody] CreatePaymentRequest request,
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

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var result = await _payments.CreateForOrderAsync(
            _currentUser.UserId.Value,
            orderId,
            request,
            keyValues.First()!.Trim(),
            cancellationToken);

        return Ok(result);
    }
}
