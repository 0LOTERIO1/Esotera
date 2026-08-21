using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;

namespace Esotera.Infrastructure.Services;

/// <summary>Fake Testing — token fixo, zero rede. Conta logins.</summary>
public sealed class FakeJ3SellerAuthProvider : IJ3SellerAuthProvider
{
    private int _getCallCount;
    private int _loginSimulatedCount;
    private string? _cached;
    private DateTimeOffset _renewAfterUtc = DateTimeOffset.MinValue;

    public int GetCallCount => _getCallCount;
    public int LoginSimulatedCount => _loginSimulatedCount;
    public string NextToken { get; set; } = "fake-seller-access-token";
    public J3SellerAuthResult? NextResultOverride { get; set; }
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);
    public bool ForceExpireOnNextGet { get; set; }

    public Task<J3SellerAuthResult> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _getCallCount);

        if (NextResultOverride is { } forced)
            return Task.FromResult(forced);

        if (ForceExpireOnNextGet)
        {
            ForceExpireOnNextGet = false;
            InvalidateCachedToken();
        }

        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(_cached) && now < _renewAfterUtc)
            return Task.FromResult(J3SellerAuthResult.Success(_cached));

        Interlocked.Increment(ref _loginSimulatedCount);
        _cached = NextToken;
        _renewAfterUtc = now.Add(CacheTtl);
        return Task.FromResult(J3SellerAuthResult.Success(_cached));
    }

    public void InvalidateCachedToken()
    {
        _cached = null;
        _renewAfterUtc = DateTimeOffset.MinValue;
    }

    public void Reset()
    {
        _getCallCount = 0;
        _loginSimulatedCount = 0;
        InvalidateCachedToken();
        NextToken = "fake-seller-access-token";
        NextResultOverride = null;
        CacheTtl = TimeSpan.FromHours(1);
        ForceExpireOnNextGet = false;
    }
}
