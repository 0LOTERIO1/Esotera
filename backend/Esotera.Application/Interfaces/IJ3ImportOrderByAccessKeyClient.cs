using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Cliente GraphQL J3 — somente mutation importOrderByAccessKey.
/// Separado de <see cref="IJ3FulfillmentClient"/> (createTmsOrders). Sem fallback entre os dois.
/// Zero retry. Não gera etiqueta.
/// </summary>
public interface IJ3ImportOrderByAccessKeyClient
{
    /// <summary>
    /// Envia <c>importOrderByAccessKey</c> no máximo uma vez.
    /// Exige <c>J3_IMPORT_BY_ACCESS_KEY_ENABLED</c>. Não decripta XML aqui — recebe parse já materializado.
    /// </summary>
    Task<J3CreateOrderAttemptResult> ImportAsync(
        Order order,
        FiscalInvoiceParseResult parsedFiscal,
        CancellationToken cancellationToken = default);
}
