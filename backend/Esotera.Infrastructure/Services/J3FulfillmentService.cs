using System.Collections.Concurrent;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Persistência Pending + claim local. Sem mutations J3 / sem HTTP externo.
/// Invariante: payment_approved AND ShippingMethodId == j3 → exatamente um J3Fulfillment.
/// Independente de J3_ENABLED e J3_FULFILLMENT_ENABLED. Zero HTTP.
/// Claim/mutations futuras: J3_FULFILLMENT_ENABLED (não exige J3_ENABLED — pedidos já pagos).
/// Claim: ExecuteUpdate atômico no PostgreSQL; fallback sincronizado no InMemory (testes).
/// Webhook→Pending: prova autoritativa é SQLite relacional (não EF InMemory).
/// Commit atômico (PaymentService, relacional): payment_approved + histórico + Pending.
/// Auditoria futura (sem worker neste passo): payment_approved + j3 + fulfillment ausente.
/// </summary>
public class J3FulfillmentService : IJ3FulfillmentService
{
    /// <summary>Gates por Id — só InMemory/testes (provider sem ExecuteUpdate).</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryClaimGates = new();

    /// <summary>Serializa EnsurePending por OrderId no InMemory (unique não é enforced).</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryEnsureGates = new();

    private readonly EsoteraDbContext _context;
    private readonly J3ShippingOptions _j3;
    private readonly ILogger<J3FulfillmentService> _logger;

    public J3FulfillmentService(
        EsoteraDbContext context,
        IOptions<J3ShippingOptions> j3Options,
        ILogger<J3FulfillmentService> logger)
    {
        _context = context;
        _j3 = j3Options.Value;
        _logger = logger;
    }

    public async Task EnsurePendingAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // InMemory: unique OrderId não é enforced — serializa por pedido (testes).
        if (!_context.Database.IsRelational())
        {
            var gate = InMemoryEnsureGates.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                await EnsurePendingCoreAsync(orderId, cancellationToken);
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        await EnsurePendingCoreAsync(orderId, cancellationToken);
    }

    /// <summary>
    /// Obrigação local: payment_approved + ShippingMethodId=j3.
    /// Independente de J3_ENABLED e J3_FULFILLMENT_ENABLED. Zero HTTP.
    /// </summary>
    private async Task EnsurePendingCoreAsync(Guid orderId, CancellationToken cancellationToken)
    {
        // Preferir instância já rastreada (ex.: webhook MP no mesmo DbContext).
        // Reconsultar AsNoTracking com Order tracked + StatusHistory pode falhar no InMemory
        // e o webhook engole a exception (HTTP 200) — o pedido fica pago sem Pending.
        var order = _context.Orders.Local.FirstOrDefault(o => o.Id == orderId)
            ?? await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return;

        if (order.Status != OrderStatus.PaymentApproved)
            return;

        if (!string.Equals(order.ShippingMethodId, ShippingMethod.J3, StringComparison.OrdinalIgnoreCase))
            return;

        var exists = await _context.J3Fulfillments
            .AsNoTracking()
            .AnyAsync(f => f.OrderId == orderId, cancellationToken);
        if (exists)
            return;

        var now = DateTime.UtcNow;
        _context.J3Fulfillments.Add(new J3Fulfillment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = J3FulfillmentStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        // 1:1 fixup marca Order Modified; SaveChanges extra no InMemory quebra o webhook MP (HTTP 200, sem Pending).
        SuppressTrackedPrincipalUpdates();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "J3Fulfillment Pending criado para pedido {OrderId} (zero HTTP J3).",
                orderId);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            var already = await _context.J3Fulfillments.AsNoTracking()
                .AnyAsync(f => f.OrderId == orderId, cancellationToken);
            if (already)
                return;

            _context.J3Fulfillments.Add(new J3Fulfillment
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = J3FulfillmentStatus.Pending,
                AttemptCount = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "J3Fulfillment Pending criado para pedido {OrderId} após retry de concorrência (zero HTTP J3).",
                orderId);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race / webhook duplicado — unique OrderId: fulfillment já registrado, não é 500.
            _logger.LogInformation(
                "J3Fulfillment já existia para pedido {OrderId} (idempotente).",
                orderId);
            _context.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Insert só do J3Fulfillment. Order/StatusHistory já persistidos pelo caller (webhook).
    /// </summary>
    private void SuppressTrackedPrincipalUpdates()
    {
        foreach (var entry in _context.ChangeTracker.Entries<Order>())
        {
            if (entry.State == EntityState.Modified)
                entry.State = EntityState.Unchanged;
        }

        foreach (var entry in _context.ChangeTracker.Entries<OrderStatusHistory>())
        {
            if (entry.State == EntityState.Modified)
                entry.State = EntityState.Unchanged;
        }
    }

    public async Task<bool> TryClaimPendingAsync(
        Guid fulfillmentId,
        CancellationToken cancellationToken = default)
    {
        // Processamento/mutation futuro: sem flag, não reivindica. Pending permanece.
        if (!_j3.FulfillmentEnabled)
            return false;

        var now = DateTime.UtcNow;

        // Produção (PostgreSQL): UPDATE atômico WHERE Status=Pending; claim termina antes de qualquer HTTP.
        if (_context.Database.IsRelational())
        {
            var rows = await _context.J3Fulfillments
                .Where(f => f.Id == fulfillmentId && f.Status == J3FulfillmentStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(f => f.Status, J3FulfillmentStatus.Processing)
                        .SetProperty(f => f.StartedAtUtc, now)
                        .SetProperty(f => f.UpdatedAtUtc, now)
                        .SetProperty(f => f.AttemptCount, f => f.AttemptCount + 1),
                    cancellationToken);

            return rows == 1;
        }

        // InMemory (testes): sem ExecuteUpdate — serializa claim por Id.
        var gate = InMemoryClaimGates.GetOrAdd(fulfillmentId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var entity = await _context.J3Fulfillments
                .FirstOrDefaultAsync(
                    f => f.Id == fulfillmentId && f.Status == J3FulfillmentStatus.Pending,
                    cancellationToken);
            if (entity is null)
                return false;

            entity.Status = J3FulfillmentStatus.Processing;
            entity.StartedAtUtc = now;
            entity.UpdatedAtUtc = now;
            entity.AttemptCount += 1;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pg)
            return pg.SqlState == PostgresErrorCodes.UniqueViolation;

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
