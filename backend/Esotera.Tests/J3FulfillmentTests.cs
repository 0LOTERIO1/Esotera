using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Esotera.Application.DTOs.Addresses;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>Passo 4.1 — persistência/claim local J3 + residencial. Zero mutation/HTTP J3.</summary>
public class J3FulfillmentTests : IClassFixture<J3FulfillmentEnabledWebApplicationFactory>
{
    private readonly J3FulfillmentEnabledWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public J3FulfillmentTests(J3FulfillmentEnabledWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void FulfillmentEnabled_Default_IsFalse_OnOptionsClass()
    {
        new J3ShippingOptions().FulfillmentEnabled.Should().BeFalse();
        new J3ShippingOptions().CanFulfill.Should().BeFalse();
    }

    [Fact]
    public void Startup_FulfillmentDisabled_IncompleteConfig_DoesNotCrash()
    {
        using var factory = new J3DisabledWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.Enabled.Should().BeFalse();
        opts.FulfillmentEnabled.Should().BeFalse();
        opts.CanFulfill.Should().BeFalse();
        // Host sobe e resolve DbContext sem ValidateOnStart de fulfillment.
        scope.ServiceProvider.GetRequiredService<EsoteraDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>().Should().NotBeNull();
    }

    [Fact]
    public async Task EnsurePending_CreatesSingle_AndDuplicateIsIdempotent()
    {
        var orderId = await SeedApprovedJ3OrderAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            await svc.EnsurePendingAsync(orderId);
            await svc.EnsurePendingAsync(orderId);
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var rows = await db.J3Fulfillments.Where(f => f.OrderId == orderId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task EnsurePending_WhenOrderAlreadyTrackedWithHistory_CreatesPending()
    {
        var orderId = await SeedApprovedJ3OrderAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        _ = await db.Orders.Include(o => o.StatusHistory).SingleAsync(o => o.Id == orderId);

        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>().EnsurePendingAsync(orderId);

        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public void UniqueOrderId_Index_ConfiguredOnModel()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var entity = db.Model.FindEntityType(typeof(J3Fulfillment));
        entity.Should().NotBeNull();
        var orderIdIndex = entity!.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(J3Fulfillment.OrderId));
        orderIdIndex.IsUnique.Should().BeTrue();

        var j3OrderIdIndex = entity.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(J3Fulfillment.J3OrderId));
        j3OrderIdIndex.IsUnique.Should().BeTrue();
        j3OrderIdIndex.GetFilter().Should().Contain("J3OrderId");
    }

    [Fact]
    public async Task EnsurePending_Concurrent_OnlyOneRecord_NoUnhandledError()
    {
        var orderId = await SeedApprovedJ3OrderAsync();

        var t1 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            await svc.EnsurePendingAsync(orderId);
        });
        var t2 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            await svc.EnsurePendingAsync(orderId);
        });

        var act = async () => await Task.WhenAll(t1, t2);
        await act.Should().NotThrowAsync();

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task DuplicateCreate_Local_DoesNotCreateTwo()
    {
        var orderId = await SeedApprovedJ3OrderAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);
        await svc.EnsurePendingAsync(orderId);
        await svc.EnsurePendingAsync(orderId);

        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Claim_PendingToProcessing_Once()
    {
        var fulfillmentId = await SeedPendingFulfillmentAsync();

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        var first = await svc.TryClaimPendingAsync(fulfillmentId);
        var second = await svc.TryClaimPendingAsync(fulfillmentId);

        first.Should().BeTrue();
        second.Should().BeFalse();

        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var row = await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
        row.Status.Should().Be(J3FulfillmentStatus.Processing);
        row.AttemptCount.Should().Be(1);
        row.StartedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Claim_Concurrent_OnlyOneWins()
    {
        var fulfillmentId = await SeedPendingFulfillmentAsync();

        var t1 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            return await svc.TryClaimPendingAsync(fulfillmentId);
        });
        var t2 = Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            return await svc.TryClaimPendingAsync(fulfillmentId);
        });

        var results = await Task.WhenAll(t1, t2);
        results.Count(x => x).Should().Be(1);
        results.Count(x => !x).Should().Be(1);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var row = await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
        row.Status.Should().Be(J3FulfillmentStatus.Processing);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Claim_Created_Fails()
    {
        var id = await SeedFulfillmentWithStatusAsync(J3FulfillmentStatus.Created);
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        (await svc.TryClaimPendingAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task Claim_UnknownOutcome_Fails()
    {
        var id = await SeedFulfillmentWithStatusAsync(J3FulfillmentStatus.UnknownOutcome);
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        (await svc.TryClaimPendingAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task Address_Residential_True_Persists()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
        var res = await _client.PostAsJsonAsync("/api/users/me/addresses", new CreateAddressRequest(
            "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
            IsPrimary: false, IsResidentialAddress: true));
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<AddressDto>(JsonOptions);
        dto!.IsResidentialAddress.Should().BeTrue();
    }

    [Fact]
    public async Task Address_Commercial_False_Persists()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
        var res = await _client.PostAsJsonAsync("/api/users/me/addresses", new CreateAddressRequest(
            "01310100", "Av Paulista", "2000", null, "Bela Vista", "São Paulo", "SP",
            IsPrimary: false, IsResidentialAddress: false));
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<AddressDto>(JsonOptions);
        dto!.IsResidentialAddress.Should().BeFalse();
    }

    [Fact]
    public async Task Address_Legacy_Null_Valid()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
        var res = await _client.PostAsJsonAsync("/api/users/me/addresses", new CreateAddressRequest(
            "01310100", "Av Paulista", "3000", null, "Bela Vista", "São Paulo", "SP",
            IsPrimary: false, IsResidentialAddress: null));
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<AddressDto>(JsonOptions);
        dto!.IsResidentialAddress.Should().BeNull();
    }

    [Fact]
    public async Task Order_Snapshot_Preserves_Residential_Null_True_False()
    {
        await ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(_factory.Services);

        foreach (var expected in new bool?[] { null, true, false })
        {
            var (token, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"res{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token);

            var orderReq = new CreateOrderRequest(
                [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
                new OrderAddressInput(
                    "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                    expected),
                null,
                ShippingMethod.MelhorEconomico,
                "pix",
                null,
                null);

            var create = await TestHelpers.PostOrderAsync(_client, orderReq);
            create.EnsureSuccessStatusCode();
            var orderDto = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderDto!.Id);
            order.ShippingIsResidentialAddress.Should().Be(expected);
        }
    }

    [Fact]
    public async Task PacSedex_Accept_Address_With_Residential_Null()
    {
        await ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(_factory.Services);

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"pac{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: null),
            null,
            ShippingMethod.MelhorEconomico,
            "pix",
            null,
            null);

        var create = await TestHelpers.PostOrderAsync(_client, orderReq);
        create.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateOrder_J3_ResidentialNull_Rejected()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"j3n{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: null),
            null,
            ShippingMethod.J3,
            "pix",
            null,
            null);

        var create = await TestHelpers.PostOrderAsync(_client, orderReq);
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await create.Content.ReadAsStringAsync();
        body.Should().Contain("Residencial");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateOrder_J3_ResidentialTrueFalse_Allowed_AndSnapshotExact(bool residential)
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"j3r{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: residential),
            null,
            ShippingMethod.J3,
            "pix",
            null,
            null);

        var create = await TestHelpers.PostOrderAsync(_client, orderReq);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderDto = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderDto!.Id);
        order.ShippingIsResidentialAddress.Should().Be(residential);
    }

    [Fact]
    public async Task EnsurePending_DoesNotCall_J3Client()
    {
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeJ3Client>();
        fake.Reset();
        var beforeCov = fake.CoverageCallCount;
        var beforeTrack = fake.TrackingCallCount;

        var orderId = await SeedApprovedJ3OrderAsync();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);
        (await svc.TryClaimPendingAsync(
            (await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.AsNoTracking()
                .SingleAsync(f => f.OrderId == orderId)).Id)).Should().BeTrue();

        fake.CoverageCallCount.Should().Be(beforeCov);
        fake.TrackingCallCount.Should().Be(beforeTrack);
    }

    [Fact]
    public void No_J3_Mutation_Methods_On_Client()
    {
        var names = typeof(IJ3Client).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
        names.Should().NotContain(n =>
            n.Contains("Create", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Stamp", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Tms", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Mutat", StringComparison.OrdinalIgnoreCase));
        names.Should().BeEquivalentTo("IsServiceAreaAsync", "GetTrackingAsync");
    }

    [Fact]
    public void MerchandiseValue_EsoteraRule_ExcludesShipping()
    {
        // Regra comercial Esotera — não regra oficial J3.
        J3MerchandiseValue.ToCents(100m, 10m).Should().Be(9000);
        J3MerchandiseValue.ToCents(5m, 10m).Should().Be(0);
    }

    [Fact]
    public void ErrorCode_Sanitize_StripsUnsafe()
    {
        J3FulfillmentErrorCodes.Sanitize("HTTP_500").Should().Be("HTTP_500");
        J3FulfillmentErrorCodes.Sanitize("token=secret!!!").Should().Be("TOKENSECRET");
        J3FulfillmentErrorCodes.Sanitize("").Should().BeNull();
        J3FulfillmentErrorCodes.Sanitize(new string('A', 100))!.Length.Should().Be(64);
    }

    [Fact]
    public async Task EnsurePending_FulfillmentDisabled_StillCreatesPending()
    {
        using var factory = new CustomWebApplicationFactory(); // fulfillment default false
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.FulfillmentEnabled.Should().BeFalse();

        var orderId = await SeedApprovedJ3OrderAsync(db);
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);

        var row = await db.J3Fulfillments.AsNoTracking().SingleAsync(f => f.OrderId == orderId);
        row.Status.Should().Be(J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task EnsurePending_J3EnabledFalse_StillCreatesPending_WhenMethodIsJ3()
    {
        using var factory = new J3DisabledWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.Enabled.Should().BeFalse();

        var orderId = await SeedApprovedJ3OrderAsync(db);
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);

        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task EnsurePending_PacSedex_DoesNotCreate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedApprovedOrderAsync(db, ShippingMethod.MelhorEconomico);
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);

        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
    }

    private async Task<Guid> SeedApprovedJ3OrderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        return await SeedApprovedOrderAsync(db, ShippingMethod.J3);
    }

    private static Task<Guid> SeedApprovedJ3OrderAsync(EsoteraDbContext db) =>
        SeedApprovedOrderAsync(db, ShippingMethod.J3);

    private static async Task<Guid> SeedApprovedOrderAsync(EsoteraDbContext db, string shippingMethodId)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"J{Guid.NewGuid():N}"[..12],
            UserId = user.Id,
            Status = OrderStatus.PaymentApproved,
            Subtotal = 50,
            Discount = 0,
            ShippingPrice = 12.99m,
            Total = 62.99m,
            ShippingMethodId = shippingMethodId,
            ShippingMethodName = shippingMethodId,
            ShippingProvider = shippingMethodId == ShippingMethod.J3 ? "J3" : "Melhor Envio",
            ShipCep = "01310100",
            ShipStreet = "Av Paulista",
            ShipNumber = "1000",
            ShipNeighborhood = "Bela Vista",
            ShipCity = "São Paulo",
            ShipState = "SP",
            ShippingIsResidentialAddress = shippingMethodId == ShippingMethod.J3 ? true : null,
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Cliente",
            CustomerEmail = user.Email,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Guid> SeedPendingFulfillmentAsync()
    {
        var orderId = await SeedApprovedJ3OrderAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
        await svc.EnsurePendingAsync(orderId);
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        return await db.J3Fulfillments.AsNoTracking()
            .Where(f => f.OrderId == orderId)
            .Select(f => f.Id)
            .SingleAsync();
    }

    private async Task<Guid> SeedFulfillmentWithStatusAsync(string status)
    {
        var orderId = await SeedApprovedJ3OrderAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
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
}

/// <summary>Confirma default da factory padrão: fulfillment off.</summary>
public class J3FulfillmentFlagDefaultTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public J3FulfillmentFlagDefaultTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void FactoryDefault_FulfillmentEnabled_False()
    {
        using var scope = _factory.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.FulfillmentEnabled.Should().BeFalse();
        opts.Enabled.Should().BeTrue(); // factory de cotação liga Enabled; fulfillment permanece false
        opts.CanFulfill.Should().BeFalse();
        opts.Ecommerce.Should().Be("Standalone");
        opts.OrderPickupType.Should().Be("Standard");
    }
}
