using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly IAdminQueryService _adminQueries;
    private readonly IOrderService _orderService;
    private readonly IUpSellerOrderExportService _upSellerExport;
    private readonly IFiscalInvoiceImportService _fiscalImport;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<UpdateOrderStatusRequest> _statusValidator;

    public AdminOrdersController(
        IAdminQueryService adminQueries,
        IOrderService orderService,
        IUpSellerOrderExportService upSellerExport,
        IFiscalInvoiceImportService fiscalImport,
        ICurrentUserService currentUser,
        IValidator<UpdateOrderStatusRequest> statusValidator)
    {
        _adminQueries = adminQueries;
        _orderService = orderService;
        _upSellerExport = upSellerExport;
        _fiscalImport = fiscalImport;
        _currentUser = currentUser;
        _statusValidator = statusValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminOrderSummaryDto>>> List(
        [FromQuery] OrderFilterRequest filter)
    {
        var result = await _adminQueries.ListOrdersAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDetailDto>> Get(Guid id)
    {
        var order = await _adminQueries.GetOrderAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    /// <summary>Download .xlsx no layout oficial UpSeller (aba order_). Sem HTTP externo.</summary>
    [HttpGet("{id:guid}/upseller-export")]
    public async Task<IActionResult> ExportUpSeller(
        Guid id,
        CancellationToken cancellationToken)
    {
        var file = await _upSellerExport.ExportOrderAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Importa XML de NF-e (emitida no UpSeller). Sem SEFAZ/J3/HTTP externo.
    /// Resposta sem XML bruto.
    /// </summary>
    [HttpPost("{id:guid}/fiscal-invoices/xml")]
    [RequestSizeLimit(FiscalInvoiceImportService.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FiscalInvoiceImportService.MaxUploadBytes)]
    public async Task<ActionResult<FiscalInvoiceImportResultDto>> ImportFiscalXml(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Arquivo obrigatório",
                Detail = "Envie o XML da NF-e no campo file.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _fiscalImport.ImportXmlAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest? request)
    {
        if (_currentUser.UserId == null)
            return Unauthorized();

        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Corpo da requisição inválido",
                Detail = "Informe o novo status do pedido.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var validation = await _statusValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ));
        }

        var order = await _orderService.UpdateStatusAsync(
            id, request, _currentUser.UserId.Value);
        return Ok(order);
    }
}
