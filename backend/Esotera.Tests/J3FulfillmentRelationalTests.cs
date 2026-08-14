using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Esotera.Application.Options;

namespace Esotera.Tests;

/// <summary>
/// Unique OrderId e claim Pending→Processing em SQLite in-memory (relacional).
/// Não usa PostgreSQL/Neon. InMemory do EF não enforce unique — por isso este fixture.
/// </summary>
public class J3FulfillmentRelationalTests
{
    [Fact]
    public async Task EnsurePending_Twice_ExactlyOneRow_Idempotent()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedApprovedOrderAsync();
        var options = Options.Create(new J3ShippingOptions { FulfillmentEnabled = false });

        await using (var db = harness.CreateContext())
        {
            var svc = new J3FulfillmentService(db, options, NullLogger<J3FulfillmentService>.Instance);
            await svc.EnsurePendingAsync(orderId);
            await svc.EnsurePendingAsync(orderId);
        }

        await using var verify = harness.CreateContext();
        var rows = await verify.J3Fulfillments.Where(f => f.OrderId == orderId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(J3FulfillmentStatus.Pending);
        rows[0].AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task Unique_OrderId_PreventsTwoFulfillmentRows()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedApprovedOrderAsync();

        await using (var db = harness.CreateContext())
        {
            db.J3Fulfillments.Add(NewPending(orderId));
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateContext())
        {
            db.J3Fulfillments.Add(NewPending(orderId));
            var act = () => db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var verify = harness.CreateContext())
        {
            (await verify.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
        }
    }

    [Fact]
    public async Task Claim_PendingToProcessing_OnlyOneWins()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedApprovedOrderAsync();
        Guid fulfillmentId;
        await using (var db = harness.CreateContext())
        {
            var row = NewPending(orderId);
            db.J3Fulfillments.Add(row);
            await db.SaveChangesAsync();
            fulfillmentId = row.Id;
        }

        var options = Options.Create(new J3ShippingOptions { FulfillmentEnabled = true });

        await using var ctx1 = harness.CreateContext();
        await using var ctx2 = harness.CreateContext();
        var svc1 = new J3FulfillmentService(ctx1, options, NullLogger<J3FulfillmentService>.Instance);
        var svc2 = new J3FulfillmentService(ctx2, options, NullLogger<J3FulfillmentService>.Instance);

        var t1 = svc1.TryClaimPendingAsync(fulfillmentId);
        var t2 = svc2.TryClaimPendingAsync(fulfillmentId);
        var results = await Task.WhenAll(t1, t2);

        results.Count(won => won).Should().Be(1);
        results.Count(won => !won).Should().Be(1);

        await using var verify = harness.CreateContext();
        var rowAfter = await verify.J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
        rowAfter.Status.Should().Be(J3FulfillmentStatus.Processing);
        rowAfter.AttemptCount.Should().Be(1);
    }

    private static J3Fulfillment NewPending(Guid orderId)
    {
        var now = DateTime.UtcNow;
        return new J3Fulfillment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = J3FulfillmentStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EsoteraDbContext> _options;

        private SqliteHarness(SqliteConnection connection, DbContextOptions<EsoteraDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connection = new SqliteConnection($"DataSource=file:j3rel_{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EsoteraDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var db = new EsoteraDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            return new SqliteHarness(connection, options);
        }

        public EsoteraDbContext CreateContext() => new(_options);

        public async Task<Guid> SeedApprovedOrderAsync()
        {
            await using var db = CreateContext();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                Name = "Rel Test",
                Email = $"rel-{userId:N}@example.com",
                PasswordHash = "x",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            var orderId = Guid.NewGuid();
            db.Orders.Add(new Order
            {
                Id = orderId,
                OrderNumber = $"REL-{orderId.ToString("N")[..8]}",
                UserId = userId,
                Status = OrderStatus.PaymentApproved,
                ShippingMethodId = "j3",
                ShippingMethodName = "J3",
                ShippingProvider = "j3",
                ShipCep = "03065000",
                ShipStreet = "Rua",
                ShipNumber = "1",
                ShipNeighborhood = "Bairro",
                ShipCity = "São Paulo",
                ShipState = "SP",
                PaymentMethod = "pix",
                PaymentStatus = "approved",
                CustomerName = "Cliente",
                CustomerEmail = "c@example.com",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return orderId;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
