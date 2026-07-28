using Esotera.Application.DTOs.Orders;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateOrderRequest> _createValidator;

    public OrdersController(
        IOrderService orderService,
        ICurrentUserService currentUser,
        IValidator<CreateOrderRequest> createValidator)
    {
        _orderService = orderService;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderListDto>>> ListMine()
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var orders = await _orderService.ListMineAsync(_currentUser.UserId.Value);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetMine(Guid id)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        var order = await _orderService.GetMineAsync(_currentUser.UserId.Value, id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderRequest request)
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

        var idempotencyKey = keyValues.First()!.Trim();

        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var order = await _orderService.CreateAsync(
            _currentUser.UserId.Value,
            request,
            idempotencyKey);

        return CreatedAtAction(nameof(GetMine), new { id = order.Id }, order);
    }
}
