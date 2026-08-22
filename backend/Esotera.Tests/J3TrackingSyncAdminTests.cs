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
/// Tracking sync manual: searchOrderByCode read-only + J3RemoteStatus.
/// Zero createTmsOrders / importOrderByAccessKey / processor.
/// </summary>
public class J3TrackingSyncAdminTests
{
    private const string J3Code = "J32605553033";
    private const string RemoteOrderId = "315f0c1e-9331-48df-8102-0a58d2cd5a07";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TrackingSyncService_DependsOnlyOnLookupClient()
    {
        var ctor = typeof(J3TrackingSyncService).GetConstructors().Single();
        var types = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        types.Should().Contain(typeof(IJ3OrderLookupClient));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
        types.Should().NotContain(typeof(IJ3ImportOrderByAccessKeyClient));
        types.Should().NotContain(typeof(IJ3FulfillmentProcessor));
        types.Should().NotContain(typeof(IJ3Client));
    }

    [Fact]
    public void AdminOrdersController_TakesTrackingSyncService()
    {
        var types = typeof(Esotera.Api.Controllers.AdminOrdersController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToArray();
        types.Should().Contain(typeof(IJ3TrackingSyncService));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
    }

    [Fact]
    public async Task Success_PersistsPendingRemoteStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;
        FakeJ3FulfillmentClient createFake;
        FakeJ3ImportOrderByAccessKeyClient importFake;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            lookup.Reset();
            createFake.Reset();
            importFake.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(scope);
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3TrackingSyncResultDto>(JsonOptions);
        body!.Outcome.Should().Be("Success");
        body.J3RemoteStatus.Should().Be("Pending");
        body.J3LastStatusSyncAtUtc.Should().NotBeNull();
        body.J3LastStatusSyncErrorCode.Should().BeNull();
        body.J3LastStatusSyncErrorAtUtc.Should().BeNull();
        body.FulfillmentStatus.Should().Be(J3FulfillmentStatus.Created);
        body.LookupHttpSent.Should().BeTrue();
        body.OperationName.Should().Be(J3SearchOrderByCodeQuery.OperationName);
        lookup.LastCode.Should().Be(J3Code);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().NotBeNull();
            row.J3LastStatusSyncErrorCode.Should().BeNull();
            row.J3LastStatusSyncErrorAtUtc.Should().BeNull();
            row.J3OrderId.Should().Be(RemoteOrderId);
            row.J3OrderCode.Should().Be(J3Code);
        }

        createFake.CreateCallCount.Should().Be(0);
        importFake.CallCount.Should().Be(0);
        lookup.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task UnknownRemoteStatus_PersistedRaw()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(scope);
            lookup.NextResult = FoundWithStatus("SomeFutureStatus");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3TrackingSyncResultDto>(JsonOptions);
        body!.J3RemoteStatus.Should().Be("SomeFutureStatus");

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("SomeFutureStatus");
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task LookupFailure_PreservesPriorRemoteSnapshot()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-2);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: priorSync);
            lookup.NextResult = J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        GetReason(problem).Should().Be(J3TrackingSyncErrorCodes.LookupFailed);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().BeCloseTo(priorSync, TimeSpan.FromSeconds(2));
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.LookupFailed);
            row.J3LastStatusSyncErrorAtUtc.Should().NotBeNull();
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }

        lookup.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task NotFound_PreservesPriorRemoteSnapshot()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-1);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: priorSync);
            lookup.NextResult = J3OrderLookupResult.NotFound();
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.NotFound);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().BeCloseTo(priorSync, TimeSpan.FromSeconds(2));
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.NotFound);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task IdMismatch_FailClosed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                j3OrderId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                remoteStatus: "Pending",
                lastSyncAt: DateTime.UtcNow.AddDays(-1));
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.IdMismatch);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3OrderId.Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.IdMismatch);
        }
    }

    [Fact]
    public async Task TrackingMismatch_FailClosed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(scope, remoteStatus: "Pending");
            lookup.NextResult = new J3OrderLookupResult
            {
                Outcome = J3OrderLookupOutcome.Found,
                Response = SampleResponse("Pending") with
                {
                    DeliveryPoints =
                    [
                        new J3SearchOrderByCodeDeliveryPointDto("Rua X", "03065-000", "OTHERCODE")
                    ]
                }
            };
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.TrackingMismatch);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.TrackingMismatch);
        }
    }

    [Fact]
    public async Task ZipMismatch_FailClosed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(scope, remoteStatus: "Pending");
            lookup.NextResult = new J3OrderLookupResult
            {
                Outcome = J3OrderLookupOutcome.Found,
                Response = SampleResponse("Pending") with
                {
                    DeliveryPoints =
                    [
                        new J3SearchOrderByCodeDeliveryPointDto("Rua X", "01310-100", J3Code)
                    ]
                }
            };
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.ZipMismatch);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.ZipMismatch);
        }
    }

    [Fact]
    public async Task PendingFulfillment_Rejected_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Pending,
                j3OrderCode: J3Code,
                tracking: J3Code);
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.NotEligible);
        lookup.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingJ3OrderCode_Rejected_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: RemoteOrderId,
                j3OrderCode: null,
                tracking: J3Code);
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.NotEligible);
        lookup.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Idempotent_TwoSuccessfulSyncs_SameStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        FakeJ3OrderLookupClient lookup;
        FakeJ3FulfillmentClient createFake;
        FakeJ3ImportOrderByAccessKeyClient importFake;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            lookup.Reset();
            createFake.Reset();
            importFake.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(scope);
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var first = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<J3TrackingSyncResultDto>(JsonOptions);
        var firstAt = firstBody!.J3LastStatusSyncAtUtc;

        await Task.Delay(20);

        var second = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<J3TrackingSyncResultDto>(JsonOptions);
        secondBody!.J3RemoteStatus.Should().Be("Pending");
        secondBody.J3LastStatusSyncAtUtc.Should().BeAfter(firstAt!.Value.AddMilliseconds(-1));
        secondBody.J3LastStatusSyncErrorCode.Should().BeNull();

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3RemoteStatus.Should().Be("Pending");
            (await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
        }

        createFake.CreateCallCount.Should().Be(0);
        importFake.CallCount.Should().Be(0);
        lookup.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task LocalCodeAndTrackingMismatch_Rejected_ZeroJ3Calls()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-3);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                j3OrderId: RemoteOrderId,
                j3OrderCode: "ABC",
                tracking: "XYZ",
                remoteStatus: "Pending",
                lastSyncAt: priorSync);
            lookup.NextResult = FoundWithStatus("Pending");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.LocalCodeMismatch);
        lookup.CallCount.Should().Be(0);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().BeCloseTo(priorSync, TimeSpan.FromSeconds(2));
            row.J3LastStatusSyncErrorCode.Should().BeNull();
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task LookupErrorThenSuccess_ClearsSyncErrorAndUpdatesRemoteStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-4);
        var priorErrAt = DateTime.UtcNow.AddHours(-1);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: priorSync,
                lastSyncErrorCode: J3TrackingSyncErrorCodes.LookupFailed,
                lastSyncErrorAt: priorErrAt);
            lookup.NextResult = FoundWithStatus("Delivered");
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3TrackingSyncResultDto>(JsonOptions);
        body!.J3RemoteStatus.Should().Be("Delivered");
        body.J3LastStatusSyncErrorCode.Should().BeNull();
        body.J3LastStatusSyncErrorAtUtc.Should().BeNull();
        body.J3LastStatusSyncAtUtc.Should().BeAfter(priorSync);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3RemoteStatus.Should().Be("Delivered");
            row.J3LastStatusSyncAtUtc.Should().BeAfter(priorSync);
            row.J3LastStatusSyncErrorCode.Should().BeNull();
            row.J3LastStatusSyncErrorAtUtc.Should().BeNull();
        }
    }

    [Fact]
    public async Task EmptyDeliveryPoints_PreservesPriorRemoteSnapshot()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-2);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: priorSync);
            lookup.NextResult = new J3OrderLookupResult
            {
                Outcome = J3OrderLookupOutcome.Found,
                Response = SampleResponse("Pending") with { DeliveryPoints = [] }
            };
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.DeliveryPointMissing);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().BeCloseTo(priorSync, TimeSpan.FromSeconds(2));
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.DeliveryPointMissing);
            row.J3LastStatusSyncErrorAtUtc.Should().NotBeNull();
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task StatusMissing_PreservesPriorRemoteSnapshot()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        var priorSync = DateTime.UtcNow.AddHours(-5);
        FakeJ3OrderLookupClient lookup;

        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: priorSync);
            lookup.NextResult = new J3OrderLookupResult
            {
                Outcome = J3OrderLookupOutcome.Found,
                Response = SampleResponse("Pending") with { Status = "   " }
            };
        }

        var response = await client.PostAsync($"/api/admin/orders/{orderId}/j3-tracking/sync", null);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3TrackingSyncErrorCodes.StatusMissing);

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.J3RemoteStatus.Should().Be("Pending");
            row.J3LastStatusSyncAtUtc.Should().BeCloseTo(priorSync, TimeSpan.FromSeconds(2));
            row.J3LastStatusSyncErrorCode.Should().Be(J3TrackingSyncErrorCodes.StatusMissing);
            row.J3LastStatusSyncErrorAtUtc.Should().NotBeNull();
            row.Status.Should().Be(J3FulfillmentStatus.Created);
        }
    }

    [Fact]
    public async Task AdminDetail_ExposesTrackingSyncFields()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid fulfillmentId;
        var syncAt = DateTime.UtcNow.AddMinutes(-5);

        using (var scope = factory.Services.CreateScope())
        {
            (_, _, fulfillmentId) = await SeedCreatedAsync(
                scope,
                remoteStatus: "Pending",
                lastSyncAt: syncAt);
        }

        var response = await client.GetAsync($"/api/admin/j3-fulfillments/{fulfillmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<J3FulfillmentAdminDetailDto>(JsonOptions);
        dto!.J3RemoteStatus.Should().Be("Pending");
        dto.J3LastStatusSyncAtUtc.Should().BeCloseTo(syncAt, TimeSpan.FromSeconds(2));
        dto.J3LastStatusSyncErrorCode.Should().BeNull();
        dto.J3LastStatusSyncErrorAtUtc.Should().BeNull();
    }

    [Fact]
    public void Identity_StatusMissing_FailClosed()
    {
        var order = new Order
        {
            CustomerName = "X",
            ShipCep = "03065000",
            Subtotal = 1,
            Discount = 0,
            ShippingPrice = 1
        };
        var fulfillment = new J3Fulfillment
        {
            J3OrderId = RemoteOrderId,
            J3OrderCode = J3Code,
            J3TrackingNumber = J3Code
        };
        var response = SampleResponse("Pending") with { Status = "   " };
        var (status, err) = J3TrackingSyncIdentity.TryValidate(order, fulfillment, response);
        status.Should().BeNull();
        err.Should().Be(J3TrackingSyncErrorCodes.StatusMissing);
    }

    private static J3OrderLookupResult FoundWithStatus(string status) =>
        new()
        {
            Outcome = J3OrderLookupOutcome.Found,
            Response = SampleResponse(status)
        };

    private static J3SearchOrderByCodeResponseDto SampleResponse(string status) =>
        new(
            RemoteOrderId,
            "2026-08-22",
            null,
            status,
            "sueli bressan martins comercio ltda",
            "Standalone",
            [
                new J3SearchOrderByCodeDeliveryPointDto(
                    "Rua Filipe Camarão, 431",
                    "03065-000",
                    J3Code)
            ]);

    private static Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedCreatedAsync(
        IServiceScope scope,
        string? j3OrderId = RemoteOrderId,
        string? remoteStatus = null,
        DateTime? lastSyncAt = null,
        string? lastSyncErrorCode = null,
        DateTime? lastSyncErrorAt = null) =>
        SeedAsync(
            scope,
            fulfillmentStatus: J3FulfillmentStatus.Created,
            j3OrderId: j3OrderId,
            j3OrderCode: J3Code,
            tracking: J3Code,
            remoteStatus: remoteStatus,
            lastSyncAt: lastSyncAt,
            lastSyncErrorCode: lastSyncErrorCode,
            lastSyncErrorAt: lastSyncErrorAt);

    private static async Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedAsync(
        IServiceScope scope,
        string fulfillmentStatus,
        string? j3OrderId = null,
        string? j3OrderCode = null,
        string? tracking = null,
        string? remoteStatus = null,
        DateTime? lastSyncAt = null,
        string? lastSyncErrorCode = null,
        DateTime? lastSyncErrorAt = null)
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
            J3RemoteStatus = remoteStatus,
            J3LastStatusSyncAtUtc = lastSyncAt,
            J3LastStatusSyncErrorCode = lastSyncErrorCode,
            J3LastStatusSyncErrorAtUtc = lastSyncErrorAt,
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
