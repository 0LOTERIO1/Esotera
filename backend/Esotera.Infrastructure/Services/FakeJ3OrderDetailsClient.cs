using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>Fake Testing — zero rede. Conta lookups. Nunca chama mutations.</summary>
public sealed class FakeJ3OrderDetailsClient : IJ3OrderDetailsClient
{
    private int _callCount;
    public int CallCount => _callCount;
    public string? LastOrderId { get; private set; }
    public J3OrderDetailsLookupResult NextResult { get; set; } = J3OrderDetailsLookupResult.NotFound();

    /// <summary>Se definido, a próxima chamada lança esta exception (após incrementar CallCount).</summary>
    public Exception? ThrowOnNextCall { get; set; }

    public Task<J3OrderDetailsLookupResult> GetByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        LastOrderId = orderId;
        if (ThrowOnNextCall is not null)
        {
            var ex = ThrowOnNextCall;
            ThrowOnNextCall = null;
            throw ex;
        }

        return Task.FromResult(NextResult);
    }

    public void Reset()
    {
        _callCount = 0;
        LastOrderId = null;
        NextResult = J3OrderDetailsLookupResult.NotFound();
        ThrowOnNextCall = null;
    }
}
