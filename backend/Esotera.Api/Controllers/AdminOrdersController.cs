using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.DTOs.J3;
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
    private readonly IJ3FulfillmentAdminProcessService _j3Process;
    private readonly IJ3ImportOrderByAccessKeyAdminService _j3ImportByAccessKey;
    private readonly IJ3ReconcileAdminService _j3Reconcile;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<UpdateOrderStatusRequest> _statusValidator;

    public AdminOrdersController(
        IAdminQueryService adminQueries,
        IOrderService orderService,
        IUpSellerOrderExportService upSellerExport,
        IFiscalInvoiceImportService fiscalImport,
        IJ3FulfillmentAdminProcessService j3Process,
        IJ3ImportOrderByAccessKeyAdminService j3ImportByAccessKey,
        IJ3ReconcileAdminService j3Reconcile,
        ICurrentUserService currentUser,
        IValidator<UpdateOrderStatusRequest> statusValidator)
    {
        _adminQueries = adminQueries;
        _orderService = orderService;
        _upSellerExport = upSellerExport;
        _fiscalImport = fiscalImport;
        _j3Process = j3Process;
        _j3ImportByAccessKey = j3ImportByAccessKey;
        _j3Reconcile = j3Reconcile;
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

    /// <summary>
    /// Ação Admin manual: eligibility → EnsurePending → Processor.
    /// Sem body. Sem HTTP J3 se flag off / inelegível. Sem XML/ChNFe/token na resposta.
    /// </summary>
    [HttpPost("{id:guid}/j3-fulfillment/process")]
    public async Task<ActionResult<J3FulfillmentAdminProcessDto>> ProcessJ3Fulfillment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var outcome = await _j3Process.ProcessOrderAsync(id, cancellationToken);
        if (outcome.HttpStatus == StatusCodes.Status404NotFound)
            return NotFound();

        if (outcome.HttpStatus == StatusCodes.Status409Conflict)
        {
            var problem = new ProblemDetails
            {
                Title = "Não foi possível processar J3",
                Detail = outcome.Message,
                Status = StatusCodes.Status409Conflict
            };
            problem.Extensions["reasonCode"] = outcome.ReasonCode;
            problem.Extensions["eligibilityReason"] = outcome.ReasonCode;
            if (outcome.Body is not null)
                problem.Extensions["fulfillment"] = outcome.Body;
            return Conflict(problem);
        }

        return Ok(outcome.Body);
    }

    /// <summary>
    /// Recovery Admin controlado: UMA chamada importOrderByAccessKey.
    /// Exige confirmação do OrderNumber. Nunca createTmsOrders. Nunca promove Created.
    /// </summary>
    [HttpPost("{id:guid}/j3-import-by-access-key")]
    public async Task<ActionResult<J3ImportByAccessKeyAdminResultDto>> ImportJ3ByAccessKey(
        Guid id,
        [FromBody] J3ImportByAccessKeyConfirmRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ConfirmOrderNumber))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Confirmação obrigatória",
                Detail = "Informe confirmOrderNumber igual ao OrderNumber do pedido.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var outcome = await _j3ImportByAccessKey.ImportAsync(id, request, cancellationToken);
        if (outcome.HttpStatus == StatusCodes.Status404NotFound)
            return NotFound();

        if (outcome.HttpStatus == StatusCodes.Status400BadRequest)
        {
            var bad = new ProblemDetails
            {
                Title = "Confirmação inválida",
                Detail = outcome.Message,
                Status = StatusCodes.Status400BadRequest
            };
            bad.Extensions["reasonCode"] = outcome.ReasonCode;
            return BadRequest(bad);
        }

        if (outcome.HttpStatus == StatusCodes.Status409Conflict)
        {
            var problem = new ProblemDetails
            {
                Title = "Não foi possível importar por chave de acesso",
                Detail = outcome.Message,
                Status = StatusCodes.Status409Conflict
            };
            problem.Extensions["reasonCode"] = outcome.ReasonCode;
            if (outcome.Body is not null)
                problem.Extensions["result"] = outcome.Body;
            return Conflict(problem);
        }

        if (outcome.HttpStatus == StatusCodes.Status422UnprocessableEntity)
        {
            var problem = new ProblemDetails
            {
                Title = "importOrderByAccessKey rejeitado",
                Detail = outcome.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            };
            problem.Extensions["reasonCode"] = outcome.ReasonCode;
            if (outcome.Body is not null)
                problem.Extensions["result"] = outcome.Body;
            return UnprocessableEntity(problem);
        }

        return Ok(outcome.Body);
    }

    /// <summary>
    /// Reconciliação admin: lookup read-only searchOrderByCode + promove J3Fulfillment unknown_outcome → Created.
    /// Zero createTmsOrders / importOrderByAccessKey. Independente das flags de mutation.
    /// </summary>
    [HttpPost("{id:guid}/j3-reconcile")]
    public async Task<ActionResult<J3ReconcileAdminResultDto>> ReconcileJ3(
        Guid id,
        [FromBody] J3ReconcileConfirmRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.ConfirmOrderNumber)
            || string.IsNullOrWhiteSpace(request.ConfirmJ3OrderCode))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Confirmação obrigatória",
                Detail = "Informe confirmOrderNumber e confirmJ3OrderCode.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var outcome = await _j3Reconcile.ReconcileAsync(id, request, cancellationToken);
        if (outcome.HttpStatus == StatusCodes.Status404NotFound)
            return NotFound();

        if (outcome.HttpStatus == StatusCodes.Status400BadRequest)
        {
            var bad = new ProblemDetails
            {
                Title = "Confirmação inválida",
                Detail = outcome.Message,
                Status = StatusCodes.Status400BadRequest
            };
            bad.Extensions["reasonCode"] = outcome.ReasonCode;
            return BadRequest(bad);
        }

        if (outcome.HttpStatus == StatusCodes.Status409Conflict)
        {
            var problem = new ProblemDetails
            {
                Title = "Não foi possível reconciliar com a J3",
                Detail = outcome.Message,
                Status = StatusCodes.Status409Conflict
            };
            problem.Extensions["reasonCode"] = outcome.ReasonCode;
            if (outcome.Body is not null)
                problem.Extensions["result"] = outcome.Body;
            return Conflict(problem);
        }

        return Ok(outcome.Body);
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
