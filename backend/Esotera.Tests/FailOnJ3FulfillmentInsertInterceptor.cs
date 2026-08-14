using Esotera.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Esotera.Tests;

/// <summary>
/// Falha puramente local: lança ao persistir J3Fulfillment Added.
/// O SaveChanges anterior (Order + histórico) já ocorreu na mesma transaction.
/// </summary>
public sealed class FailOnJ3FulfillmentInsertInterceptor : SaveChangesInterceptor
{
    public const string Message = "test: J3Fulfillment insert failed";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ThrowIfInsertingFulfillment(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ThrowIfInsertingFulfillment(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ThrowIfInsertingFulfillment(DbContext? context)
    {
        if (context is null)
            return;
        if (context.ChangeTracker.Entries<J3Fulfillment>().Any(e => e.State == EntityState.Added))
            throw new InvalidOperationException(Message);
    }
}
