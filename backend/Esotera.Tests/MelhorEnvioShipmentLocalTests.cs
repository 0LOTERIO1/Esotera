using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esotera.Tests;

/// <summary>
/// Fase B: registro LOCAL do envio Melhor Envio. Nenhum teste aqui pode exercitar HTTP —
/// o serviço não tem cliente do Melhor Envio injetado, o que é a garantia estrutural.
/// SQLite relacional para que unique index e filtered index sejam realmente enforced.
/// </summary>
public class MelhorEnvioShipmentLocalTests
{
    [Fact]
    public async Task ApprovedMelhorEnvioOrder_WithoutInvoice_CreatesWaitingInvoice()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.WaitingInvoice);
    }

    [Fact]
    public async Task ApprovedMelhorEnvioOrder_WithAuthorizedInvoice_CreatesReadyToCreate()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);
        await harness.SeedInvoiceAsync(orderId, FiscalInvoiceStatus.Authorized);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.ReadyToCreate);
    }

    [Fact]
    public async Task UnknownInvoice_IsNotEnoughForReadyToCreate()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorEconomico,
            OrderStatus.PaymentApproved);
        // XML importado mas sem prova de autorização — envio comercial não pode prosseguir.
        await harness.SeedInvoiceAsync(orderId, FiscalInvoiceStatus.Unknown);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.WaitingInvoice);
    }

    [Fact]
    public async Task J3Order_DoesNotCreateMelhorEnvioShipment()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.J3,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        (await verify.MelhorEnvioShipments.AnyAsync(s => s.OrderId == orderId))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.AwaitingPayment)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task OrderNotApproved_DoesNotCreateShipment(string status)
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(ShippingMethod.MelhorExpresso, status);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        (await verify.MelhorEnvioShipments.AnyAsync(s => s.OrderId == orderId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task EnsureTwice_IsIdempotent_ExactlyOneRow()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
        {
            var svc = harness.CreateService(db);
            await svc.EnsureAsync(orderId);
            await svc.EnsureAsync(orderId);
        }

        // Contexto separado também não duplica (unique OrderId).
        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        (await verify.MelhorEnvioShipments.CountAsync(s => s.OrderId == orderId))
            .Should().Be(1);
    }

    [Fact]
    public async Task UniqueOrderId_PreventsTwoShipmentRows()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
        {
            db.MelhorEnvioShipments.Add(NewWaiting(orderId));
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateContext())
        {
            db.MelhorEnvioShipments.Add(NewWaiting(orderId));
            var act = () => db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        await using var verify = harness.CreateContext();
        (await verify.MelhorEnvioShipments.CountAsync(s => s.OrderId == orderId))
            .Should().Be(1);
    }

    [Fact]
    public async Task SyncInvoiceReadiness_PromotesWaitingInvoiceToReadyToCreate()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await harness.SeedInvoiceAsync(orderId, FiscalInvoiceStatus.Authorized);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).SyncInvoiceReadinessAsync(orderId);

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.ReadyToCreate);
    }

    [Fact]
    public async Task SyncInvoiceReadiness_WithoutAuthorizedInvoice_StaysWaiting()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
        {
            var svc = harness.CreateService(db);
            await svc.EnsureAsync(orderId);
            await svc.SyncInvoiceReadinessAsync(orderId);
        }

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.WaitingInvoice);
    }

    [Theory]
    [InlineData(MelhorEnvioShipmentStatus.CartCreated)]
    [InlineData(MelhorEnvioShipmentStatus.Purchased)]
    [InlineData(MelhorEnvioShipmentStatus.LabelGenerated)]
    [InlineData(MelhorEnvioShipmentStatus.Failed)]
    [InlineData(MelhorEnvioShipmentStatus.Cancelled)]
    public async Task SyncInvoiceReadiness_NeverDowngradesOrReopensStatus(string status)
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);
        await harness.SeedInvoiceAsync(orderId, FiscalInvoiceStatus.Authorized);

        await using (var db = harness.CreateContext())
        {
            var row = NewWaiting(orderId);
            row.Status = status;
            db.MelhorEnvioShipments.Add(row);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).SyncInvoiceReadinessAsync(orderId);

        await using var verify = harness.CreateContext();
        var after = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        after.Status.Should().Be(status);
    }

    [Fact]
    public async Task Ensure_CopiesQuoteSnapshotAndQuoteEnvironment()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var verify = harness.CreateContext();
        var row = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);

        row.ServiceId.Should().Be(2);
        row.ServiceName.Should().Be("SEDEX");
        row.CarrierName.Should().Be("Correios");
        row.SelectedDisplayName.Should().Be("Melhor Envio - Expresso");
        row.QuotedPrice.Should().Be(31.90m);
        row.ChargedFreightPrice.Should().Be(24.90m);
        row.DeliveryTimeDays.Should().Be(3);
        // O ambiente vem da cotação do pedido, não do options — o envio nasce onde foi cotado.
        row.Environment.Should().Be("production");

        // Nada remoto pode existir sem chamada ao Melhor Envio.
        row.MelhorEnvioShipmentId.Should().BeNull();
        row.MelhorEnvioProtocol.Should().BeNull();
        row.TrackingCode.Should().BeNull();
        row.LabelUrl.Should().BeNull();
        row.CartCreatedAtUtc.Should().BeNull();
        row.PurchasedAtUtc.Should().BeNull();
        row.LabelGeneratedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AdminOrderDetail_ExposesMelhorEnvioShipment()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.MelhorExpresso,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var db2 = harness.CreateContext();
        var detail = await new AdminQueryService(db2).GetOrderAsync(orderId);

        detail.Should().NotBeNull();
        detail!.MelhorEnvio.Should().NotBeNull();
        detail.MelhorEnvio!.Status.Should().Be(MelhorEnvioShipmentStatus.WaitingInvoice);
        detail.MelhorEnvio.Environment.Should().Be("production");
        detail.MelhorEnvio.ShipmentId.Should().BeNull();
        detail.MelhorEnvio.TrackingCode.Should().BeNull();
        detail.MelhorEnvio.LabelUrl.Should().BeNull();
    }

    [Fact]
    public async Task AdminOrderDetail_J3Order_HasNoMelhorEnvioBlock()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var orderId = await harness.SeedOrderAsync(
            ShippingMethod.J3,
            OrderStatus.PaymentApproved);

        await using (var db = harness.CreateContext())
            await harness.CreateService(db).EnsureAsync(orderId);

        await using var db2 = harness.CreateContext();
        var detail = await new AdminQueryService(db2).GetOrderAsync(orderId);

        detail.Should().NotBeNull();
        detail!.MelhorEnvio.Should().BeNull();
    }

    private static MelhorEnvioShipment NewWaiting(Guid orderId)
    {
        var now = DateTime.UtcNow;
        return new MelhorEnvioShipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Environment = "production",
            Status = MelhorEnvioShipmentStatus.WaitingInvoice,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EsoteraDbContext> _options;

        private SqliteHarness(
            SqliteConnection connection,
            DbContextOptions<EsoteraDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connection = new SqliteConnection(
                $"DataSource=file:merel_{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EsoteraDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var db = new EsoteraDbContext(options))
                await db.Database.EnsureCreatedAsync();

            return new SqliteHarness(connection, options);
        }

        public EsoteraDbContext CreateContext() => new(_options);

        /// <summary>Options em sandbox de propósito: o ambiente correto deve vir da cotação.</summary>
        public MelhorEnvioShipmentLocalService CreateService(EsoteraDbContext db) =>
            new(
                db,
                Options.Create(new MelhorEnvioOptions { Environment = "sandbox" }),
                NullLogger<MelhorEnvioShipmentLocalService>.Instance);

        public async Task<Guid> SeedOrderAsync(string shippingMethodId, string status)
        {
            await using var db = CreateContext();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                Name = "ME Test",
                Email = $"me-{userId:N}@example.com",
                PasswordHash = "x",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            var orderId = Guid.NewGuid();
            db.Orders.Add(new Order
            {
                Id = orderId,
                OrderNumber = $"ME-{orderId.ToString("N")[..8]}",
                UserId = userId,
                Status = status,
                ShippingMethodId = shippingMethodId,
                ShippingMethodName = ShippingMethod.GetDisplayName(shippingMethodId),
                ShippingProvider = ShippingMethod.GetProvider(shippingMethodId),
                ShippingPrice = 24.90m,
                ShippingOriginalPrice = 31.90m,
                ShippingEstimatedDays = 3,
                ShippingCompanyId = 1,
                ShippingServiceId = 2,
                ShippingCarrierName = "Correios",
                ShippingServiceName = "SEDEX",
                ShippingQuoteEnvironment = "production",
                ShippingQuotedAtUtc = DateTime.UtcNow,
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

        public async Task SeedInvoiceAsync(Guid orderId, string status)
        {
            await using var db = CreateContext();
            db.FiscalInvoices.Add(new FiscalInvoice
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = status,
                ChNFe = status == FiscalInvoiceStatus.Authorized
                    ? new string('1', 44)
                    : null,
                XmlCipher = "cipher",
                XmlSha256 = Guid.NewGuid().ToString("N"),
                Source = FiscalInvoiceSource.ManualUpload,
                AuthorizedAtUtc = status == FiscalInvoiceStatus.Authorized
                    ? DateTime.UtcNow
                    : null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
