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

/// <summary>
/// Hidratação manual: getOrderDetails read-only → J3OrderCode + J3TrackingNumber.
/// Zero createTmsOrders / importOrderByAccessKey / processor / tracking sync.
/// </summary>
public class J3IdentifierHydrationAdminTests
{
    private const string Tracking = "J32657369171";
    private const string RemoteOrderId = "f19b045f-9207-4037-873e-2c84d51c05ec";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void HydrationService_DependsOnlyOnOrderDetailsClient()
    {
        var ctor = typeof(J3IdentifierHydrationService).GetConstructors().Single();
        var types = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        types.Should().Contain(typeof(IJ3OrderDetailsClient));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
        types.Should().NotContain(typeof(IJ3ImportOrderByAccessKeyClient));
        types.Should().NotContain(typeof(IJ3OrderLookupClient));
        types.Should().NotContain(typeof(IJ3FulfillmentProcessor));
        types.Should().NotContain(typeof(IJ3Client));
        types.Should().NotContain(typeof(IJ3TrackingSyncService));
    }

    [Fact]
    public void AdminOrdersController_TakesHydrationService()
    {
        var types = typeof(Esotera.Api.Controllers.AdminOrdersController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToArray();
        types.Should().Contain(typeof(IJ3IdentifierHydrationService));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
    }

    [Fact]
    public async Task Success_PersistsCodeAndTracking_KeepsCreated()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderDetailsClient details;
        FakeJ3FulfillmentClient createFake;
        FakeJ3ImportOrderByAccessKeyClient importFake;
        FakeJ3OrderLookupClient lookupFake;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            lookupFake = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            details.Reset();
            createFake.Reset();
            importFake.Reset();
            lookupFake.Reset();
            (orderId, _, fulfillmentId) = await SeedNeedsHydrationAsync(scope);
            details.NextResult = FoundDetails(Tracking);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3IdentifierHydrationResultDto>(JsonOptions);
        body!.Outcome.Should().Be("Success");
        body.J3OrderCode.Should().Be(Tracking);
        body.J3TrackingNumber.Should().Be(Tracking);
        body.FulfillmentStatus.Should().Be(J3FulfillmentStatus.Created);
        body.LookupHttpSent.Should().BeTrue();
        body.OperationName.Should().Be(J3GetOrderDetailsQuery.OperationName);
        details.LastOrderId.Should().Be(RemoteOrderId);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3OrderId.Should().Be(RemoteOrderId);
            row.J3OrderCode.Should().Be(Tracking);
            row.J3TrackingNumber.Should().Be(Tracking);
            row.J3RemoteStatus.Should().BeNull();
            row.AttemptCount.Should().Be(1);
        }

        createFake.CreateCallCount.Should().Be(0);
        importFake.CallCount.Should().Be(0);
        lookupFake.CallCount.Should().Be(0);
        details.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task NotCreated_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Pending,
                j3OrderId: RemoteOrderId);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        GetReason(problem).Should().Be(J3IdentifierHydrationErrorCodes.NotEligible);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingJ3OrderId_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: null);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.NotEligible);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LookupFailure_PreservesNullIdentifiers()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, fulfillmentId) = await SeedNeedsHydrationAsync(scope);
            details.NextResult = J3OrderDetailsLookupResult.Failed(
                J3IdentifierHydrationErrorCodes.LookupFailed);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.LookupFailed);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3OrderId.Should().Be(RemoteOrderId);
            row.J3OrderCode.Should().BeNull();
            row.J3TrackingNumber.Should().BeNull();
        }
    }

    [Fact]
    public async Task NotFound_PreservesNullIdentifiers()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, fulfillmentId) = await SeedNeedsHydrationAsync(scope);
            details.NextResult = J3OrderDetailsLookupResult.NotFound();
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.NotFound);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3OrderCode.Should().BeNull();
            row.J3TrackingNumber.Should().BeNull();
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task TrackingBlank_FailClosed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, fulfillmentId) = await SeedNeedsHydrationAsync(scope);
            details.NextResult = FoundDetails("   ");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.TrackingMissing);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3OrderCode.Should().BeNull();
            row.J3TrackingNumber.Should().BeNull();
        }
    }

    [Fact]
    public async Task AlreadyHydrated_Idempotent_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: RemoteOrderId,
                j3OrderCode: Tracking,
                tracking: Tracking);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3IdentifierHydrationResultDto>(JsonOptions);
        body!.Outcome.Should().Be("AlreadyHydrated");
        body.LookupHttpSent.Should().BeFalse();
        body.J3OrderCode.Should().Be(Tracking);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LocalCodeTrackingDiverge_FailClosed_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: RemoteOrderId,
                j3OrderCode: "ABC",
                tracking: "XYZ");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.LocalConflict);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task PartialLocalIdentifiers_FailClosed_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: RemoteOrderId,
                j3OrderCode: Tracking,
                tracking: null);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.LocalConflict);
        details.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task IdMismatch_FailClosed_NoPersist()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderDetailsClient details;

        using (var scope = factory.Services.CreateScope())
        {
            details = scope.ServiceProvider.GetRequiredService<FakeJ3OrderDetailsClient>();
            details.Reset();
            (orderId, _, fulfillmentId) = await SeedNeedsHydrationAsync(scope);
            details.NextResult = FoundDetails(Tracking, remoteId: "00000000-0000-0000-0000-000000000099");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-identifiers/hydrate", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3IdentifierHydrationErrorCodes.IdMismatch);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3OrderCode.Should().BeNull();
            row.J3TrackingNumber.Should().BeNull();
        }
    }

    [Fact]
    public void Identity_ZipMismatch_FailClosed()
    {
        var order = new Order
        {
            CustomerName = "X",
            ShipCep = "03065000",
            Subtotal = 1,
            Discount = 0,
            ShippingPrice = 1
        };
        var fulfillment = new J3Fulfillment { J3OrderId = RemoteOrderId };
        var response = new J3OrderDetailsDto(
            RemoteOrderId,
            "Pending",
            new J3DeliveryPointDetailsDto(
                Guid.NewGuid().ToString(),
                Tracking,
                "01000-000",
                "Outro"));
        var (tracking, err) = J3IdentifierHydrationIdentity.TryValidate(order, fulfillment, response);
        tracking.Should().BeNull();
        err.Should().Be(J3IdentifierHydrationErrorCodes.ZipMismatch);
    }

    [Fact]
    public void Query_DoesNotInventFields()
    {
        J3GetOrderDetailsQuery.Document.Should().Contain("getOrderDetails");
        J3GetOrderDetailsQuery.Document.Should().Contain("trackingNumber");
        J3GetOrderDetailsQuery.Document.Should().Contain("addressZipCode");
        J3GetOrderDetailsQuery.Document.Should().NotContain("orderCode");
        J3GetOrderDetailsQuery.Document.Should().NotContain("stampUrl");
        J3GetOrderDetailsQuery.Document.Should().NotContain("createTmsOrders");
    }

    private static J3OrderDetailsLookupResult FoundDetails(
        string tracking,
        string? remoteId = RemoteOrderId) =>
        J3OrderDetailsLookupResult.Found(
            new J3OrderDetailsDto(
                remoteId!,
                "Pending",
                new J3DeliveryPointDetailsDto(
                    "dp-" + Guid.NewGuid().ToString("N")[..8],
                    tracking,
                    "03065-000",
                    "Rua Filipe Camarão, 431")));

    private static Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedNeedsHydrationAsync(
        IServiceScope scope) =>
        SeedAsync(
            scope,
            fulfillmentStatus: J3FulfillmentStatus.Created,
            j3OrderId: RemoteOrderId,
            j3OrderCode: null,
            tracking: null);

    private static async Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedAsync(
        IServiceScope scope,
        string fulfillmentStatus,
        string? j3OrderId = null,
        string? j3OrderCode = null,
        string? tracking = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");
        var now = DateTime.UtcNow;
        var orderNumber = $"ES{now:yyMMddHHmmss}{Random.Shared.Next(10, 99)}";
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            UserId = user.Id,
            Status = OrderStatus.PaymentApproved,
            Subtotal = 54.9m,
            Discount = 0,
            ShippingPrice = 12.99m,
            Total = 67.89m,
            ShippingMethodId = ShippingMethod.J3,
            ShippingMethodName = "J3",
            ShippingProvider = "J3",
            ShipCep = "03065000",
            ShipStreet = "Rua Filipe Camarão",
            ShipNumber = "431",
            ShipNeighborhood = "Bairro",
            ShipCity = "São Paulo",
            ShipState = "SP",
            ShippingIsResidentialAddress = true,
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Pedro Lotério dos Santos",
            CustomerEmail = user.Email,
            CustomerPhone = "11988887777",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Orders.Add(order);
        var fulfillmentId = Guid.NewGuid();
        db.J3Fulfillments.Add(new J3Fulfillment
        {
            Id = fulfillmentId,
            OrderId = order.Id,
            Status = fulfillmentStatus,
            J3OrderId = j3OrderId,
            J3OrderCode = j3OrderCode,
            J3TrackingNumber = tracking,
            AttemptCount = 1,
            StartedAtUtc = now,
            CompletedAtUtc = fulfillmentStatus == J3FulfillmentStatus.Created ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        return (order.Id, order.OrderNumber, fulfillmentId);
    }

    private static async Task SetAdminAsync(HttpClient client)
    {
        var token = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, token);
    }

    private static string? GetReason(JsonElement problem)
    {
        if (problem.TryGetProperty("reasonCode", out var top) && top.ValueKind == JsonValueKind.String)
            return top.GetString();
        if (problem.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object
            && ext.TryGetProperty("reasonCode", out var r) && r.ValueKind == JsonValueKind.String)
            return r.GetString();
        return null;
    }
}
