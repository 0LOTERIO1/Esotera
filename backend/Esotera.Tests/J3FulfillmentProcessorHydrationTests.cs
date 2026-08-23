using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// TRACK-1D: após create Success + MarkCreated (SaveChanges), hydration best-effort.
/// Falha de hydration nunca reverte created nem dispara mutation.
/// </summary>
public class J3FulfillmentProcessorHydrationTests : IClassFixture<J3FulfillmentEnabledWebApplicationFactory>
{
    private const string RemoteOrderId = "f19b045f-9207-4037-873e-2c84d51c05ec";
    private const string Tracking = "J32657369171";

    private readonly J3FulfillmentEnabledWebApplicationFactory _factory;

    public J3FulfillmentProcessorHydrationTests(J3FulfillmentEnabledWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public void Processor_DependsOnHydration_NotOnOrderDetailsClientDirectly()
    {
        var ctor = typeof(J3FulfillmentProcessor).GetConstructors().Single();
        var types = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        types.Should().Contain(typeof(IJ3IdentifierHydrationService));
        types.Should().Contain(typeof(IJ3FulfillmentClient));
        types.Should().NotContain(typeof(IJ3OrderDetailsClient));
        types.Should().NotContain(typeof(IJ3OrderLookupClient));
        types.Should().NotContain(typeof(IJ3ImportOrderByAccessKeyClient));
        types.Should().NotContain(typeof(IJ3TrackingSyncService));
    }

    [Fact]
    public async Task A_CreateSuccess_HydrationSuccess_PersistsIdentifiers()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = FoundDetails(Tracking, RemoteOrderId, zip: "01310-100");

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be(RemoteOrderId);
        row.J3OrderCode.Should().Be(Tracking);
        row.J3TrackingNumber.Should().Be(Tracking);
        row.AttemptCount.Should().Be(1);
        row.CompletedAtUtc.Should().NotBeNull();
        row.J3RemoteStatus.Should().BeNull();

        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
        details.LastOrderId.Should().Be(RemoteOrderId);
    }

    [Fact]
    public async Task B_CreateSuccess_HydrationLookupFailure_PreservesCreated()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = J3OrderDetailsLookupResult.Failed(
            J3IdentifierHydrationErrorCodes.LookupFailed);

        var fid = await SeedPendingAsync();
        var completedBeforeHydrationProbe = await ProcessAndCaptureCompletedAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be(RemoteOrderId);
        row.J3OrderCode.Should().BeNull();
        row.J3TrackingNumber.Should().BeNull();
        row.CompletedAtUtc.Should().BeCloseTo(completedBeforeHydrationProbe!.Value, TimeSpan.FromSeconds(2));

        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task C_CreateSuccess_HydrationNotFound_PreservesCreated()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = J3OrderDetailsLookupResult.NotFound();

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be(RemoteOrderId);
        row.J3OrderCode.Should().BeNull();
        row.J3TrackingNumber.Should().BeNull();
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task D_CreateSuccess_TrackingMissing_PreservesCreated()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = FoundDetails("   ", RemoteOrderId, zip: "01310-100");

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderCode.Should().BeNull();
        row.J3TrackingNumber.Should().BeNull();
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task E_CreateSuccess_CepMismatch_NoIdentifiers_NoExtraMutation()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = FoundDetails(Tracking, RemoteOrderId, zip: "03065-000");

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be(RemoteOrderId);
        row.J3OrderCode.Should().BeNull();
        row.J3TrackingNumber.Should().BeNull();
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task F_CreateFailure_HydrationNotCalled()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.DefiniteFailure(
            J3FulfillmentErrorCodes.GraphqlValidation);

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.RetryableFailure);
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task G_UnknownOutcome_HydrationNotCalled()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.TimeoutUnknown);

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task H_CreateSuccess_ExactlyOneMutation()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = FoundDetails(Tracking, RemoteOrderId, zip: "01310-100");

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task I_HydrationDoesNotChangeOrderStatus()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = FoundDetails(Tracking, RemoteOrderId, zip: "01310-100");

        var fid = await SeedPendingAsync();
        Guid orderId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            orderId = await db.J3Fulfillments.AsNoTracking()
                .Where(f => f.Id == fid)
                .Select(f => f.OrderId)
                .SingleAsync();
        }

        await ProcessAsync(fid);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            order.Status.Should().Be(OrderStatus.PaymentApproved);
            var row = await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fid);
            row.J3OrderCode.Should().Be(Tracking);
        }
    }

    [Fact]
    public async Task J_CompletedAtUtc_UnchangedByHydrationFailure()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.NextResult = J3OrderDetailsLookupResult.Failed(
            J3IdentifierHydrationErrorCodes.LookupFailed);

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.CompletedAtUtc.Should().NotBeNull();
        var completed = row.CompletedAtUtc!.Value;

        // Second process should no-op (not pending) — CompletedAtUtc stays.
        await ProcessAsync(fid);
        var again = await LoadAsync(fid);
        again.CompletedAtUtc.Should().Be(completed);
        again.Status.Should().Be(J3FulfillmentStatus.Created);
        create.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAlreadyReturnsCodes_AlreadyHydrated_NoDetailsCall()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(
            RemoteOrderId, Tracking, Tracking, "dp-1");

        var fid = await SeedPendingAsync();
        await ProcessAsync(fid);

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderCode.Should().Be(Tracking);
        row.J3TrackingNumber.Should().Be(Tracking);
        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateSuccess_HydrationThrows_PreservesCreatedAndDoesNotRetryMutation()
    {
        var (create, details) = ResetFakes();
        create.NextResult = J3CreateOrderAttemptResult.Success(RemoteOrderId, null, null, null);
        details.ThrowOnNextCall = new InvalidOperationException("unexpected hydration failure");

        var fid = await SeedPendingAsync();
        var act = async () => await ProcessAsync(fid);
        await act.Should().NotThrowAsync();

        var row = await LoadAsync(fid);
        row.Status.Should().Be(J3FulfillmentStatus.Created);
        row.J3OrderId.Should().Be(RemoteOrderId);
        row.J3OrderCode.Should().BeNull();
        row.J3TrackingNumber.Should().BeNull();
        row.AttemptCount.Should().Be(1);
        row.CompletedAtUtc.Should().NotBeNull();
        row.Status.Should().NotBe(J3FulfillmentStatus.UnknownOutcome);
        row.Status.Should().NotBe(J3FulfillmentStatus.RetryableFailure);
        row.Status.Should().NotBe(J3FulfillmentStatus.Pending);
        row.Status.Should().NotBe(J3FulfillmentStatus.Processing);

        create.CreateCallCount.Should().Be(1);
        details.CallCount.Should().Be(1);

        // Sem segunda mutation em reprocess (já created).
        await ProcessAsync(fid);
        create.CreateCallCount.Should().Be(1);
        (await LoadAsync(fid)).Status.Should().Be(J3FulfillmentStatus.Created);
    }

    private (FakeJ3FulfillmentClient Create, FakeJ3OrderDetailsClient Details) ResetFakes()
    {
        using var scope = _factory.Services.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
        create.Reset();
        details.Reset();
        return (create, details);
    }

    private async Task ProcessAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>()
            .ProcessAsync(fulfillmentId);
    }

    private async Task<DateTime?> ProcessAndCaptureCompletedAsync(Guid fulfillmentId)
    {
        await ProcessAsync(fulfillmentId);
        return (await LoadAsync(fulfillmentId)).CompletedAtUtc;
    }

    private async Task<J3Fulfillment> LoadAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        return await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
    }

    private async Task<Guid> SeedPendingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedJ3OrderAsync(db);
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>().EnsurePendingAsync(orderId);
        return await db.J3Fulfillments.AsNoTracking()
            .Where(f => f.OrderId == orderId)
            .Select(f => f.Id)
            .SingleAsync();
    }

    private static J3OrderDetailsLookupResult FoundDetails(
        string tracking,
        string remoteId,
        string zip) =>
        J3OrderDetailsLookupResult.Found(
            new J3OrderDetailsDto(
                remoteId,
                "Pending",
                new J3DeliveryPointDetailsDto(
                    "dp-" + Guid.NewGuid().ToString("N")[..8],
                    tracking,
                    zip,
                    "Av Paulista, 1000")));

    private static async Task<Guid> SeedApprovedJ3OrderAsync(EsoteraDbContext db)
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

        var hex = Guid.NewGuid().ToString("N");
        Span<char> digits = stackalloc char[44];
        "35260820".AsSpan().CopyTo(digits);
        for (var i = 8; i < 44; i++)
            digits[i] = (char)('0' + (hex[(i - 8) % hex.Length] % 10));

        db.FiscalInvoices.Add(new FiscalInvoice
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = FiscalInvoiceStatus.Authorized,
            ChNFe = new string(digits),
            Number = "2",
            Series = "9",
            AuthorizedAtUtc = DateTime.UtcNow,
            XmlCipher = "test-cipher-not-real",
            XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
            Source = FiscalInvoiceSource.ManualUpload,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return order.Id;
    }
}
