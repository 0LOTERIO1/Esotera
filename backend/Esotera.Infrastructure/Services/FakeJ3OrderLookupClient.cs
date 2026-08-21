using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>Fake Testing — zero rede. Conta lookups. Nunca chama mutations.</summary>
public sealed class FakeJ3OrderLookupClient : IJ3OrderLookupClient
{
    private int _callCount;
    public int CallCount => _callCount;
    public string? LastCode { get; private set; }
    public J3OrderLookupResult NextResult { get; set; } = J3OrderLookupResult.NotFound();

    public Task<J3OrderLookupResult> SearchByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _callCount);
        LastCode = orderCode;
        return Task.FromResult(NextResult);
    }

    public void Reset()
    {
        _callCount = 0;
        LastCode = null;
        NextResult = J3OrderLookupResult.NotFound();
    }
}
