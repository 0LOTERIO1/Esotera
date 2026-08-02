using Esotera.Application.Common;
using Esotera.Application.DTOs.Shipping;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public class ShippingController : ControllerBase
{
    private readonly IShippingOptionsService _shippingOptions;
    private readonly EsoteraDbContext _db;
    private readonly IValidator<ShippingQuoteRequest> _validator;

    public ShippingController(
        IShippingOptionsService shippingOptions,
        EsoteraDbContext db,
        IValidator<ShippingQuoteRequest> validator)
    {
        _shippingOptions = shippingOptions;
        _db = db;
        _validator = validator;
    }

    /// <summary>
    /// Cotação pública. Frontend envia apenas CEP, UF e subtotal de produtos.
    /// Nunca expõe token, credencial ou resposta bruta Melhor Envio.
    /// </summary>
    [HttpPost("quote")]
    [AllowAnonymous]
    [EnableRateLimiting("shipping-quote")]
    public async Task<ActionResult<ShippingQuoteResponse>> Quote(
        [FromBody] ShippingQuoteRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ShippingQuoteResponse(
                false,
                [],
                "invalid_payload",
                "Payload inválido."));
        }

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var first = validation.Errors.FirstOrDefault();
            var code = first?.PropertyName?.Contains("Cep", StringComparison.OrdinalIgnoreCase) == true
                ? "invalid_cep"
                : "invalid_payload";
            return BadRequest(new ShippingQuoteResponse(
                false,
                [],
                code,
                first?.ErrorMessage ?? "Dados inválidos."));
        }

        var cepDigits = BrazilianCep.TryNormalize(request.DestinationCep)!;
        var settings = await _db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? StoreSettingsService.CreateDefault();

        var options = await _shippingOptions.GetAvailableOptionsAsync(
            new ShippingQuoteQuery(
                cepDigits,
                request.State.Trim().ToUpperInvariant(),
                request.ProductsSubtotal),
            settings,
            cancellationToken);

        var dtos = options.Select(MapOption).ToArray();
        if (dtos.Length == 0)
        {
            return Ok(new ShippingQuoteResponse(
                false,
                [],
                "no_options",
                "Nenhuma modalidade de entrega disponível para este endereço."));
        }

        return Ok(new ShippingQuoteResponse(true, dtos, null, null));
    }

    private static ShippingQuoteOptionDto MapOption(NormalizedShippingOption o) =>
        new(
            o.ShippingMethodId,
            o.Provider,
            o.Name,
            o.FinalPrice,
            o.OriginalPrice,
            o.EstimatedDaysLabel,
            o.EstimatedDaysMin,
            o.EstimatedDaysMax,
            o.Description,
            o.FreeShippingApplied,
            o.SubsidyApplied);
}
