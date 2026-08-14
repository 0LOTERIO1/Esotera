using Esotera.Domain.Enums;

namespace Esotera.Application.Shipping;

/// <summary>
/// Flags diagnósticas do admin J3. Somente leitura — não alteram status.
/// </summary>
public static class J3FulfillmentAdminFlags
{
    public static bool CanRetrySafely(string status) =>
        status == J3FulfillmentStatus.RetryableFailure;

    public static bool IsPossiblyStuck(string status, DateTime updatedAtUtc, DateTime utcNow, int staleMinutes)
    {
        if (status != J3FulfillmentStatus.Processing)
            return false;
        var window = TimeSpan.FromMinutes(Math.Clamp(staleMinutes, 1, 24 * 60));
        return utcNow - updatedAtUtc >= window;
    }

    public static bool NeedsManualReview(string status, bool isPossiblyStuck) =>
        status == J3FulfillmentStatus.UnknownOutcome || isPossiblyStuck;
}
