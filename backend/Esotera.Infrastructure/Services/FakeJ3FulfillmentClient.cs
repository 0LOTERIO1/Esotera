using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente mutativo fake (Testing). Zero HTTP. NÃO registrado em Production.
/// </summary>
public sealed class FakeJ3FulfillmentClient : IJ3FulfillmentClient
{
    private int _createCallCount;

    public int CreateCallCount => _createCallCount;

    public J3CreateOrderAttemptResult NextResult { get; set; } =
        J3CreateOrderAttemptResult.Success("j3-order-fake", "CODE-FAKE", "TRK-FAKE", "dp-fake");

    public Guid? LastOrderId { get; private set; }

    public J3FiscalEligibilitySnapshot? LastFiscal { get; private set; }

    public Task<J3CreateOrderAttemptResult> CreateOrderAsync(
        Order order,
        StoreSettings settings,
        J3FiscalEligibilitySnapshot? fiscal,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _createCallCount);
        LastOrderId = order.Id;
        LastFiscal = fiscal;
        return Task.FromResult(NextResult);
    }

    public void Reset()
    {
        _createCallCount = 0;
        LastOrderId = null;
        LastFiscal = null;
        NextResult = J3CreateOrderAttemptResult.Success("j3-order-fake", "CODE-FAKE", "TRK-FAKE", "dp-fake");
    }
}
