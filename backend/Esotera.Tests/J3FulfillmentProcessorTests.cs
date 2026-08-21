using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Esotera.Application.Options;

namespace Esotera.Tests;

/// <summary>Processor J3 com FakeJ3FulfillmentClient. Zero HTTP real.</summary>
public class J3FulfillmentProcessorTests : IClassFixture<J3FulfillmentEnabledWebApplicationFactory>
{
    private readonly J3FulfillmentEnabledWebApplicationFactory _factory;

    public J3FulfillmentProcessorTests(J3FulfillmentEnabledWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public void PaymentService_DoesNotTakeProcessor_NoBackgroundService()
    {
        typeof(PaymentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IJ3FulfillmentProcessor));

        typeof(J3FulfillmentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IJ3FulfillmentClient));

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetServices<IHostedService>()
            .Select(s => s.GetType().Name)
            .Should().NotContain(n => n.Contains("J3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FlagFalse_PendingUnchanged_ClientNotCalled()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        fake.Reset();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db);
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);
        var fid = await db.J3Fulfillments.AsNoTracking().Where(f => f.OrderId == orderId).Select(f => f.Id).SingleAsync();

        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>().ProcessAsync(fid);

        var row = await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fid);
        row.Status.Should().Be(J3FulfillmentStatus.Pending);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Pending_FlagTrue_ClaimsAndCallsClientOnce()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        fake.CreateCallCount.Should().Be(1);
        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Pending_PassesFiscalSnapshotToClient_WithoutXmlCipher()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        fake.CreateCallCount.Should().Be(1);
        fake.LastFiscal.Should().NotBeNull();
        fake.LastFiscal!.Status.Should().Be(FiscalInvoiceStatus.Authorized);
        fake.LastFiscal.Number.Should().Be("2");
        fake.LastFiscal.Series.Should().Be("9");
        fake.LastFiscal.ChNFe.Should().NotBeNullOrWhiteSpace();
        fake.LastFiscal.ChNFe!.Length.Should().Be(44);
        typeof(J3FiscalEligibilitySnapshot).GetProperty("XmlCipher").Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_OnlyOneClientCall()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync();

        var t1 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>().ProcessAsync(fid);
        });
        var t2 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>().ProcessAsync(fid);
        });
        await Task.WhenAll(t1, t2);

        fake.CreateCallCount.Should().Be(1);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Created);
    }

    [Fact]
    public async Task Success_PersistsIds_NoStamp()
    {
        var fake = ResetFake();
        fake.NextResult = J3CreateOrderAttemptResult.Success("oid-1", "code-1", "trk-1", "dp-1");
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be("oid-1");
        row.J3OrderCode.Should().Be("code-1");
        row.J3TrackingNumber.Should().Be("trk-1");
        row.J3DeliveryPointId.Should().Be("dp-1");
        row.J3StampUrl.Should().BeNull();
        row.CompletedAtUtc.Should().NotBeNull();
        row.LastErrorCode.Should().BeNull();
        row.AttemptCount.Should().Be(1);

        var snap = await SnapshotAsync(fid);
        snap!.J3OrderId.Should().Be("oid-1");
        snap.LastErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task DefiniteFailure_GoesRetryable_AndIsNotAutoReprocessed()
    {
        var fake = ResetFake();
        fake.NextResult = J3CreateOrderAttemptResult.DefiniteFailure(J3FulfillmentErrorCodes.GraphqlValidation);
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.RetryableFailure);
        row.LastErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlValidation);
        fake.CreateCallCount.Should().Be(1);

        fake.NextResult = J3CreateOrderAttemptResult.Success("x", "y", "z", "d");
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(1);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.RetryableFailure);
    }

    [Fact]
    public async Task UnknownOutcome_IsTerminal_NoSecondCall_NeverPending()
    {
        var fake = ResetFake();
        fake.NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.TimeoutUnknown);
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        row.LastErrorCode.Should().Be(J3FulfillmentErrorCodes.TimeoutUnknown);
        fake.CreateCallCount.Should().Be(1);

        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(1);
        (await LoadAsync(fid)).Status.Should().NotBe(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task TimeoutFake_UnknownOutcome()
    {
        ResetFake().NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.TimeoutUnknown);
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
    }

    [Fact]
    public async Task Http500Fake_UnknownOutcome()
    {
        ResetFake().NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Http500);
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
    }

    [Fact]
    public async Task SuccessFalseFake_UnknownOutcome()
    {
        ResetFake().NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.SuccessFalse);
        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
    }

    [Fact]
    public async Task InvalidCep_SkippedBeforeClaim_ZeroHttp()
    {
        var fake = ResetFake();
        var fid = await SeedPendingWithOrderAsync(o => o.ShipCep = "123");
        await ProcessAsync(fid);

        fake.CreateCallCount.Should().Be(0);
        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Pending);
        row.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task ResidentialNull_SkippedBeforeClaim_ZeroHttp()
    {
        var fake = ResetFake();
        var fid = await SeedPendingWithOrderAsync(o => o.ShippingIsResidentialAddress = null);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Pending);
        row.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingFiscalInvoice_SkippedBeforeClaim_ZeroHttp()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync(withFiscal: false);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task FiscalUnknown_SkippedBeforeClaim_ZeroHttp()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync(withFiscal: true, fiscalStatus: FiscalInvoiceStatus.Unknown);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task InvalidChNFe_SkippedBeforeClaim_ZeroHttp()
    {
        var fake = ResetFake();
        var fid = await SeedPendingAsync(withFiscal: true, chNFe: new string('1', 43) + "A");
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task OrderNotJ3_DoesNotProcess()
    {
        var fake = ResetFake();
        var fid = await SeedPendingWithOrderAsync(o => o.ShippingMethodId = ShippingMethod.MelhorEconomico);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task PaymentNotApproved_DoesNotProcess()
    {
        var fake = ResetFake();
        var fid = await SeedPendingWithOrderAsync(o => o.Status = OrderStatus.AwaitingPayment);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task ClaimLost_Processing_ClientNotCalled()
    {
        var fake = ResetFake();
        var fid = await SeedFulfillmentStatusAsync(J3FulfillmentStatus.Processing);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Processing);
    }

    [Fact]
    public async Task ExistingCreated_ClientNotCalled()
    {
        var fake = ResetFake();
        var fid = await SeedFulfillmentStatusAsync(J3FulfillmentStatus.Created);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExistingUnknownOutcome_ClientNotCalled()
    {
        var fake = ResetFake();
        var fid = await SeedFulfillmentStatusAsync(J3FulfillmentStatus.UnknownOutcome);
        await ProcessAsync(fid);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task J3EnabledFalse_StillProcesses_WhenFulfillmentEnabled()
    {
        using var factory = new J3FulfillmentOnlyWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.Enabled.Should().BeFalse();
        opts.FulfillmentEnabled.Should().BeTrue();

        var fake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        fake.Reset();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db, withFiscal: true);
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>().EnsurePendingAsync(orderId);
        var fid = await db.J3Fulfillments.AsNoTracking().Where(f => f.OrderId == orderId).Select(f => f.Id).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>().ProcessAsync(fid);

        fake.CreateCallCount.Should().Be(1);
        (await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fid))
            .Status.Should().Be(J3FulfillmentStatus.Created);
    }

    private FakeJ3FulfillmentClient ResetFake()
    {
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        fake.Reset();
        return fake;
    }

    private async Task ProcessAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>().ProcessAsync(fulfillmentId);
    }

    private async Task<J3Fulfillment> LoadAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        return await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
    }

    private async Task<Esotera.Application.DTOs.J3.J3FulfillmentAdminDto?> SnapshotAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>()
            .GetSnapshotAsync(fulfillmentId);
    }

    private async Task<Guid> SeedPendingAsync(
        bool withFiscal = true,
        string fiscalStatus = FiscalInvoiceStatus.Authorized,
        string? chNFe = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db, withFiscal, fiscalStatus, chNFe);
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>().EnsurePendingAsync(orderId);
        return await db.J3Fulfillments.AsNoTracking().Where(f => f.OrderId == orderId).Select(f => f.Id).SingleAsync();
    }

    private async Task<Guid> SeedPendingWithOrderAsync(Action<Order> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db, withFiscal: true);
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        mutate(order);
        await db.SaveChangesAsync();
        db.J3Fulfillments.Add(new J3Fulfillment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = J3FulfillmentStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return await db.J3Fulfillments.AsNoTracking().Where(f => f.OrderId == orderId).Select(f => f.Id).SingleAsync();
    }

    private async Task<Guid> SeedFulfillmentStatusAsync(string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db, withFiscal: true);
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.J3Fulfillments.Add(new J3Fulfillment
        {
            Id = id,
            OrderId = orderId,
            Status = status,
            AttemptCount = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> SeedApprovedJ3OrderAsync(
        EsoteraDbContext db,
        bool withFiscal = true,
        string fiscalStatus = FiscalInvoiceStatus.Authorized,
        string? chNFe = null)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"P{Guid.NewGuid():N}"[..12],
            UserId = user.Id,
            Status = OrderStatus.PaymentApproved,
            Subtotal = 50,
            Discount = 0,
            ShippingPrice = 12.99m,
            Total = 62.99m,
            ShippingMethodId = ShippingMethod.J3,
            ShippingMethodName = "J3",
            ShippingProvider = "J3",
            ShipCep = "01310100",
            ShipStreet = "Av Paulista",
            ShipNumber = "1000",
            ShipNeighborhood = "Bela Vista",
            ShipCity = "São Paulo",
            ShipState = "SP",
            ShippingIsResidentialAddress = true,
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Cliente",
            CustomerEmail = user.Email,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        if (withFiscal)
        {
            var key = chNFe ?? NewSyntheticChNFe();
            db.FiscalInvoices.Add(new FiscalInvoice
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = fiscalStatus,
                ChNFe = key,
                Number = "2",
                Series = "9",
                AuthorizedAtUtc = fiscalStatus == FiscalInvoiceStatus.Authorized ? DateTime.UtcNow : null,
                XmlCipher = "test-cipher-not-real",
                XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
                Source = FiscalInvoiceSource.ManualUpload,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        return order.Id;
    }

    private static string NewSyntheticChNFe()
    {
        Span<char> digits = stackalloc char[44];
        "35260820".AsSpan().CopyTo(digits);
        var hex = Guid.NewGuid().ToString("N");
        for (var i = 8; i < 44; i++)
            digits[i] = (char)('0' + (hex[(i - 8) % hex.Length] % 10));
        return new string(digits);
    }
}
