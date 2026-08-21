using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Infrastructure.Services;

/// <summary>Fake Testing — zero HTTP. Não registrado em Production.</summary>
public sealed class FakeJ3ImportOrderByAccessKeyClient : IJ3ImportOrderByAccessKeyClient
{
    private int _callCount;
    public int CallCount => _callCount;
    public J3CreateOrderAttemptResult NextResult { get; set; } =
        J3CreateOrderAttemptResult.Success("imported", null, null, null);
    public Guid? LastOrderId { get; private set; }
    public FiscalInvoiceParseResult? LastParsed { get; private set; }

    public Task<J3CreateOrderAttemptResult> ImportAsync(
        Order order,
        FiscalInvoiceParseResult parsedFiscal,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        LastOrderId = order.Id;
        LastParsed = parsedFiscal;
        return Task.FromResult(NextResult);
    }

    public void Reset()
    {
        _callCount = 0;
        LastOrderId = null;
        LastParsed = null;
        NextResult = J3CreateOrderAttemptResult.Success("imported", null, null, null);
    }
}
