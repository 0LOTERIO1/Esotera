using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

/// <summary>J3-2 — POST Admin process. Zero HTTP J3 real.</summary>
public class J3FulfillmentAdminProcessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public J3FulfillmentAdminProcessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Process_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Process_Customer_Returns403()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"j3proc{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var response = await _client.PostAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Process_UnknownOrder_Returns404()
    {
        await SetAdminAsync();
        var response = await _client.PostAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Process_FeatureDisabled_ZeroHttp_Conflict()
    {
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        fake.Reset();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var orderId = await SeedEligibleOrderAsync(db);

        await SetAdminAsync();
        var response = await _client.PostAsync(
            $"/api/admin/orders/{orderId}/j3-fulfillment/process",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        problem.GetProperty("detail").GetString().Should().Contain("desabilitada");
        GetReason(problem).Should().Be(J3FulfillmentEligibilityCodes.FeatureDisabled);
        fake.CreateCallCount.Should().Be(0);
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
    }

    [Fact]
    public async Task Process_WrongShipping_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, mutate: o =>
                o.ShippingMethodId = ShippingMethod.MelhorExpresso);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.WrongShippingMethod);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_AwaitingPayment_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        using (var s = enabled.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, mutate: o => o.Status = OrderStatus.AwaitingPayment);
            s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>().Reset();
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.PaymentNotApproved);
    }

    [Fact]
    public async Task Process_MissingFiscal_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, withFiscal: false);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.MissingFiscalInvoice);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_FiscalUnknown_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, fiscalStatus: FiscalInvoiceStatus.Unknown);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_InvalidChNFe_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, chNFe: new string('1', 43) + "A");
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.InvalidNfeKey);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_IncompleteAddress_Conflict()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, mutate: o => o.ShipStreet = " ");
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.IncompleteShippingAddress);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_EligibleWithoutRow_CreatesPendingAndCallsClient()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            fake.NextResult = J3CreateOrderAttemptResult.Success("oid-a", "code-a", "trk-a", "dp-a");
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db);
            (await db.J3Fulfillments.CountAsync(f => f.OrderId == oid)).Should().Be(0);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3FulfillmentAdminProcessDto>(JsonOptions);
        body!.Processed.Should().BeTrue();
        body.Status.Should().Be(J3FulfillmentStatus.Created);
        body.J3OrderId.Should().Be("oid-a");
        body.FulfillmentId.Should().NotBeNull();
        fake.CreateCallCount.Should().Be(1);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("test-cipher");
        json.Should().NotContain("XmlCipher");
        json.Should().NotContain("Bearer");
        json.ToLowerInvariant().Should().NotContain("chNFe".ToLowerInvariant());
    }

    [Fact]
    public async Task Process_Created_SecondPost_NoClient()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db);
        }

        (await client.PostAsync($"/api/admin/orders/{oid}/j3-fulfillment/process", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        fake.CreateCallCount.Should().Be(1);

        var second = await client.PostAsync($"/api/admin/orders/{oid}/j3-fulfillment/process", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<J3FulfillmentAdminProcessDto>(JsonOptions);
        body!.Status.Should().Be(J3FulfillmentStatus.Created);
        body.Processed.Should().BeFalse();
        fake.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Process_Processing_Conflict_NoClient()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, fulfillmentStatus: J3FulfillmentStatus.Processing);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_UnknownOutcome_Conflict_NoClient()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, fulfillmentStatus: J3FulfillmentStatus.UnknownOutcome);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_RetryableFailure_NoRetry()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        var client = enabled.CreateClient();
        await SetAdminAsync(client);
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db, fulfillmentStatus: J3FulfillmentStatus.RetryableFailure);
        }

        var response = await client.PostAsync(
            $"/api/admin/orders/{oid}/j3-fulfillment/process",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentEligibilityCodes.RetryableFailureNotAutoRetried);
        fake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_Concurrent_ClientAtMostOnce()
    {
        using var enabled = new J3FulfillmentEnabledWebApplicationFactory();
        await SetAdminAsync(enabled.CreateClient());
        Guid oid;
        FakeJ3FulfillmentClient fake;
        using (var s = enabled.Services.CreateScope())
        {
            fake = s.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            fake.Reset();
            fake.NextResult = J3CreateOrderAttemptResult.Success("c-oid", "c-code", "c-trk", "c-dp");
            var db = s.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            oid = await SeedEligibleOrderAsync(db);
        }

        var c1 = enabled.CreateClient();
        var c2 = enabled.CreateClient();
        await SetAdminAsync(c1);
        await SetAdminAsync(c2);

        var t1 = c1.PostAsync($"/api/admin/orders/{oid}/j3-fulfillment/process", null);
        var t2 = c2.PostAsync($"/api/admin/orders/{oid}/j3-fulfillment/process", null);
        await Task.WhenAll(t1, t2);

        fake.CreateCallCount.Should().BeLessThanOrEqualTo(1);
        using var verify = enabled.Services.CreateScope();
        var count = await verify.ServiceProvider.GetRequiredService<EsoteraDbContext>()
            .J3Fulfillments.CountAsync(f => f.OrderId == oid);
        count.Should().Be(1);
    }

    [Fact]
    public void AdminOrdersController_TakesProcessService_NotHttpClient()
    {
        var types = typeof(Esotera.Api.Controllers.AdminOrdersController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToArray();
        types.Should().Contain(typeof(IJ3FulfillmentAdminProcessService));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
        types.Should().NotContain(typeof(IJ3Client));
    }

    private async Task SetAdminAsync(HttpClient? client = null)
    {
        var c = client ?? _client;
        var token = await TestHelpers.GetAdminTokenAsync(c);
        TestHelpers.SetBearerToken(c, token);
    }

    private static string? GetReason(JsonElement problem)
    {
        if (problem.TryGetProperty("reasonCode", out var top) && top.ValueKind == JsonValueKind.String)
            return top.GetString();
        if (problem.TryGetProperty("eligibilityReason", out var elig) && elig.ValueKind == JsonValueKind.String)
            return elig.GetString();
        if (problem.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object)
        {
            if (ext.TryGetProperty("reasonCode", out var r) && r.ValueKind == JsonValueKind.String)
                return r.GetString();
            if (ext.TryGetProperty("eligibilityReason", out var e) && e.ValueKind == JsonValueKind.String)
                return e.GetString();
        }

        return null;
    }

    private static async Task<Guid> SeedEligibleOrderAsync(
        EsoteraDbContext db,
        bool withFiscal = true,
        string fiscalStatus = FiscalInvoiceStatus.Authorized,
        string? chNFe = null,
        string? fulfillmentStatus = null,
        Action<Order>? mutate = null)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"JP{Guid.NewGuid():N}"[..12],
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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        mutate?.Invoke(order);
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
                AuthorizedAtUtc = fiscalStatus == FiscalInvoiceStatus.Authorized ? now : null,
                XmlCipher = "test-cipher-not-real",
                XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
                Source = FiscalInvoiceSource.ManualUpload,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        if (fulfillmentStatus is not null)
        {
            db.J3Fulfillments.Add(new J3Fulfillment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = fulfillmentStatus,
                AttemptCount = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
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
