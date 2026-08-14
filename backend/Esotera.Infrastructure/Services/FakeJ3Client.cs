using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente J3 fake para ambiente Testing — sem HTTP real.
/// NÃO registrado em Production. NÃO usar como fallback de falha da API.
/// </summary>
public sealed class FakeJ3Client : IJ3Client
{
    public int CoverageCallCount { get; private set; }
    public int TrackingCallCount { get; private set; }
    public bool CoverageResult { get; set; } = true;
    public Exception? CoverageException { get; set; }
    public J3TrackingResult? TrackingResult { get; set; }
    public Exception? TrackingException { get; set; }
    public string? LastZipCode { get; private set; }
    public string? LastTrackingNumber { get; private set; }

    /// <summary>Quando definido, tem precedência sobre <see cref="CoverageResult"/>.</summary>
    public Func<string, bool>? CoverageByZip { get; set; }

    public Task<bool> IsServiceAreaAsync(string zipCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CoverageCallCount++;
        LastZipCode = zipCode;
        if (CoverageException is not null)
            throw CoverageException;
        if (CoverageByZip is not null)
            return Task.FromResult(CoverageByZip(zipCode));
        return Task.FromResult(CoverageResult);
    }

    public Task<J3TrackingResult?> GetTrackingAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TrackingCallCount++;
        LastTrackingNumber = trackingNumber;
        if (TrackingException is not null)
            throw TrackingException;
        return Task.FromResult(TrackingResult);
    }

    public void Reset()
    {
        CoverageCallCount = 0;
        TrackingCallCount = 0;
        CoverageResult = true;
        CoverageException = null;
        TrackingResult = null;
        TrackingException = null;
        LastZipCode = null;
        LastTrackingNumber = null;
        CoverageByZip = null;
    }
}
