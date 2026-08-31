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
/// Persistência local do envio Melhor Envio. Sem HTTP para o Melhor Envio.
/// Invariante: payment_approved AND frete Melhor Envio → exatamente um MelhorEnvioShipment.
/// Espelha o padrão de J3FulfillmentService (idempotência por unique OrderId, sem worker,
/// sem auto-retry) para que o processador futuro herde as mesmas garantias.
/// </summary>
public class MelhorEnvioShipmentLocalService : IMelhorEnvioShipmentLocalService
{
    /// <summary>Serializa EnsureAsync por OrderId no InMemory (unique não é enforced).</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> InMemoryEnsureGates = new();

    private readonly EsoteraDbContext _context;
    private readonly MelhorEnvioOptions _options;
    private readonly ILogger<MelhorEnvioShipmentLocalService> _logger;

    public MelhorEnvioShipmentLocalService(
        EsoteraDbContext context,
        IOptions<MelhorEnvioOptions> options,
        ILogger<MelhorEnvioShipmentLocalService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
        {
            var gate = InMemoryEnsureGates.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                await EnsureCoreAsync(orderId, cancellationToken);
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        await EnsureCoreAsync(orderId, cancellationToken);
    }

    private async Task EnsureCoreAsync(Guid orderId, CancellationToken cancellationToken)
    {
        // Preferir instância já rastreada (webhook MP no mesmo DbContext) — reconsultar
        // AsNoTracking com Order tracked pode falhar no InMemory e o webhook engole a exception.
        var order = _context.Orders.Local.FirstOrDefault(o => o.Id == orderId)
            ?? await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return;

        if (order.Status != OrderStatus.PaymentApproved)
            return;

        if (!ShippingMethod.IsMelhorEnvio(order.ShippingMethodId))
            return;

        var exists = await _context.MelhorEnvioShipments
            .AsNoTracking()
            .AnyAsync(s => s.OrderId == orderId, cancellationToken);
        if (exists)
        {
            // Já registrado: só reavalia a prontidão fiscal (idempotente).
            await SyncInvoiceReadinessAsync(orderId, cancellationToken);
            return;
        }

        var invoiceAuthorized = await HasAuthorizedInvoiceAsync(orderId, cancellationToken);

        try
        {
            AddShipment(order, invoiceAuthorized);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "MelhorEnvioShipment criado para pedido {OrderId} com status {Status} (zero HTTP Melhor Envio).",
                orderId,
                invoiceAuthorized
                    ? MelhorEnvioShipmentStatus.ReadyToCreate
                    : MelhorEnvioShipmentStatus.WaitingInvoice);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            var already = await _context.MelhorEnvioShipments
                .AsNoTracking()
                .AnyAsync(s => s.OrderId == orderId, cancellationToken);
            if (already)
                return;

            AddShipment(order, invoiceAuthorized);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "MelhorEnvioShipment criado para pedido {OrderId} após retry de concorrência.",
                orderId);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race / webhook duplicado — unique OrderId: envio já registrado, não é 500.
            _logger.LogInformation(
                "MelhorEnvioShipment já existia para pedido {OrderId} (idempotente).",
                orderId);
            _context.ChangeTracker.Clear();
        }
    }

    private void AddShipment(Order order, bool invoiceAuthorized)
    {
        var now = DateTime.UtcNow;
        _context.MelhorEnvioShipments.Add(new MelhorEnvioShipment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            // O ambiente da cotação é o que vale: o envio precisa nascer onde o frete foi cotado.
            Environment = string.IsNullOrWhiteSpace(order.ShippingQuoteEnvironment)
                ? _options.NormalizedEnvironment
                : order.ShippingQuoteEnvironment.Trim(),
            Status = invoiceAuthorized
                ? MelhorEnvioShipmentStatus.ReadyToCreate
                : MelhorEnvioShipmentStatus.WaitingInvoice,
            ServiceId = order.ShippingServiceId,
            ServiceName = order.ShippingServiceName,
            CarrierName = order.ShippingCarrierName,
            SelectedDisplayName = order.ShippingMethodName,
            QuotedPrice = order.ShippingOriginalPrice,
            ChargedFreightPrice = order.ShippingPrice,
            DeliveryTimeDays = order.ShippingEstimatedDays,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        // 1:1 fixup marca Order Modified; SaveChanges extra no InMemory quebra o webhook MP.
        SuppressTrackedPrincipalUpdates();
    }

    public async Task SyncInvoiceReadinessAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _context.MelhorEnvioShipments
            .FirstOrDefaultAsync(s => s.OrderId == orderId, cancellationToken);

        // Só promove waiting_invoice. Nunca rebaixa status avançado nem reabre failed/cancelled.
        if (shipment is null || shipment.Status != MelhorEnvioShipmentStatus.WaitingInvoice)
            return;

        if (!await HasAuthorizedInvoiceAsync(orderId, cancellationToken))
            return;

        shipment.Status = MelhorEnvioShipmentStatus.ReadyToCreate;
        shipment.UpdatedAtUtc = DateTime.UtcNow;
        SuppressTrackedPrincipalUpdates();
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "MelhorEnvioShipment do pedido {OrderId} promovido a ready_to_create (NF-e autorizada).",
            orderId);
    }

    private Task<bool> HasAuthorizedInvoiceAsync(Guid orderId, CancellationToken cancellationToken) =>
        _context.FiscalInvoices
            .AsNoTracking()
            .AnyAsync(
                f => f.OrderId == orderId && f.Status == FiscalInvoiceStatus.Authorized,
                cancellationToken);

    /// <summary>
    /// Grava só o MelhorEnvioShipment. Order/StatusHistory já são persistidos pelo caller.
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

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pg)
            return pg.SqlState == PostgresErrorCodes.UniqueViolation;

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
