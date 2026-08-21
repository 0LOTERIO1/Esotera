using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.J3;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// Reconciliação com schema REAL searchOrderByCode. Zero mutations J3.
/// </summary>
public class J3ReconcileAdminTests
{
    private const string J3Code = "J32605553033";
    private const string RemoteOrderId = "315f0c1e-9331-48df-8102-0a58d2cd5a07";

    private const string RealJson =
        """
        {"data":{"searchOrderByCode":{
          "id":"315f0c1e-9331-48df-8102-0a58d2cd5a07",
          "date":"2026-08-22",
          "nf":null,
          "status":"Pending",
          "storeName":"sueli bressan martins comercio ltda",
          "ecommerce":"Standalone",
          "deliveryPoints":[{
            "addressName":"Rua Filipe Camarão, 431",
            "addressZipCode":"03065-000",
            "trackingNumber":"J32605553033"
          }]
        }}}
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void QueryDocument_IsExactVerifiedSchema()
    {
        J3SearchOrderByCodeQuery.Document.Should().Contain("searchOrderByCode(code: $code)");
        J3SearchOrderByCodeQuery.Document.Should().Contain("deliveryPoints");
        J3SearchOrderByCodeQuery.Document.Should().Contain("addressZipCode");
        J3SearchOrderByCodeQuery.Document.Should().NotContain("orderCode");
        J3SearchOrderByCodeQuery.Document.Should().NotContain("stampUrl");
        J3SearchOrderByCodeQuery.Document.Should().NotContain("sellerId");
        J3SearchOrderByCodeQuery.Document.Should().NotContain("totalPackageValueInCents");
    }

    [Fact]
    public async Task LookupHttpClient_ParsesRealProductionJson()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RealJson, Encoding.UTF8, "application/json")
            });
        var auth = new FakeJ3SellerAuthProvider { NextToken = "seller-lookup-token" };
        var client = new J3OrderLookupHttpClient(
            new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) },
            Options.Create(new J3ShippingOptions
            {
                GraphQlUrl = "http://localhost/j3-graphql-test/",
                Token = "legacy",
                LoginEmail = "a@b.c",
                LoginPassword = "x",
                CompanyGroupCode = "J3"
            }),
            auth,
            NullLogger<J3OrderLookupHttpClient>.Instance);

        var result = await client.SearchByCodeAsync(J3Code);
        result.Outcome.Should().Be(J3OrderLookupOutcome.Found);
        result.Response.Should().NotBeNull();
        result.Response!.Id.Should().Be(RemoteOrderId);
        result.Response.Nf.Should().BeNull();
        result.Response.Status.Should().Be("Pending");
        result.Response.StoreName.Should().Be("sueli bressan martins comercio ltda");
        result.Response.Ecommerce.Should().Be("Standalone");
        result.Response.DeliveryPoints.Should().HaveCount(1);
        result.Response.DeliveryPoints[0].TrackingNumber.Should().Be(J3Code);
        result.Response.DeliveryPoints[0].AddressZipCode.Should().Be("03065-000");
    }

    [Fact]
    public void Matcher_RealPayload_MapsCanonicalFields_NfNullOk()
    {
        var response = DeserializeReal();
        var order = SampleOrder(cep: "03065000");
        var (snap, err) = J3ReconcileMatcher.TryBuildSnapshot(order, response, J3Code);
        err.Should().BeNull();
        snap.Should().NotBeNull();
        snap!.OrderId.Should().Be(RemoteOrderId);
        snap.OrderCode.Should().Be(J3Code);
        snap.TrackingNumber.Should().Be(J3Code);
        snap.DeliveryCepDigits.Should().Be("03065000");
        snap.Nf.Should().BeNull();
    }

    [Fact]
    public void Matcher_CepWithHyphen_MatchesLocalDigits()
    {
        var response = DeserializeReal();
        var (snap, err) = J3ReconcileMatcher.TryBuildSnapshot(
            SampleOrder(cep: "03065-000"), response, J3Code);
        err.Should().BeNull();
        snap!.DeliveryCepDigits.Should().Be("03065000");
    }

    [Fact]
    public void Matcher_EmptyDeliveryPoints_FailClosed()
    {
        var response = DeserializeReal() with { DeliveryPoints = [] };
        var (_, err) = J3ReconcileMatcher.TryBuildSnapshot(SampleOrder(), response, J3Code);
        err.Should().Be(J3ReconcileErrorCodes.DeliveryPointMissing);
    }

    [Fact]
    public void Matcher_TrackingNull_FailClosed()
    {
        var response = DeserializeReal() with
        {
            DeliveryPoints =
            [
                new J3SearchOrderByCodeDeliveryPointDto("Rua X", "03065-000", null)
            ]
        };
        var (_, err) = J3ReconcileMatcher.TryBuildSnapshot(SampleOrder(), response, J3Code);
        err.Should().Be(J3ReconcileErrorCodes.TrackingMismatch);
    }

    [Fact]
    public void Matcher_TrackingDifferent_FailClosed()
    {
        var response = DeserializeReal() with
        {
            DeliveryPoints =
            [
                new J3SearchOrderByCodeDeliveryPointDto("Rua X", "03065-000", "OTHER")
            ]
        };
        var (_, err) = J3ReconcileMatcher.TryBuildSnapshot(SampleOrder(), response, J3Code);
        err.Should().Be(J3ReconcileErrorCodes.TrackingMismatch);
    }

    [Fact]
    public void Matcher_CepDifferent_FailClosed()
    {
        var response = DeserializeReal() with
        {
            DeliveryPoints =
            [
                new J3SearchOrderByCodeDeliveryPointDto("Rua X", "01310-100", J3Code)
            ]
        };
        var (_, err) = J3ReconcileMatcher.TryBuildSnapshot(SampleOrder(), response, J3Code);
        err.Should().Be(J3ReconcileErrorCodes.CepMismatch);
    }

    [Fact]
    public void Matcher_AmbiguousDeliveryPoints_FailClosed()
    {
        var response = DeserializeReal() with
        {
            DeliveryPoints =
            [
                new J3SearchOrderByCodeDeliveryPointDto("A", "03065-000", J3Code),
                new J3SearchOrderByCodeDeliveryPointDto("B", "03065-000", J3Code)
            ]
        };
        var (_, err) = J3ReconcileMatcher.TryBuildSnapshot(SampleOrder(), response, J3Code);
        err.Should().Be(J3ReconcileErrorCodes.Multiple);
    }

    [Fact]
    public async Task UnknownOutcome_Ambiguous_ReconcilesToCreated()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        string orderNumber;
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
            (orderId, orderNumber, fulfillmentId) = await SeedAsync(scope);
            lookup.NextResult = FoundResult();
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-reconcile",
            new J3ReconcileConfirmRequest(orderNumber, J3Code));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3ReconcileAdminResultDto>(JsonOptions);
        body!.FulfillmentStatus.Should().Be(J3FulfillmentStatus.Created);
        body.J3OrderId.Should().Be(RemoteOrderId);
        body.J3OrderCode.Should().Be(J3Code);
        body.J3TrackingNumber.Should().Be(J3Code);
        body.FulfillmentLastErrorCode.Should().BeNull();
        body.AlreadyReconciled.Should().BeFalse();

        using (var scope = factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
            row.Status.Should().Be(J3FulfillmentStatus.Created);
            row.J3OrderId.Should().Be(RemoteOrderId);
            row.J3OrderCode.Should().Be(J3Code);
            row.J3TrackingNumber.Should().Be(J3Code);
            row.J3DeliveryPointId.Should().BeNull();
            row.J3StampUrl.Should().BeNull();
            row.LastErrorCode.Should().BeNull();
            (await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
                .J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
        }

        createFake.CreateCallCount.Should().Be(0);
        importFake.CallCount.Should().Be(0);
        lookup.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Created_SameIdAndCode_Idempotent()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        string orderNumber;
        FakeJ3OrderLookupClient lookup;
        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, orderNumber, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                lastErrorCode: null,
                j3OrderId: RemoteOrderId,
                j3OrderCode: J3Code,
                tracking: J3Code);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-reconcile",
            new J3ReconcileConfirmRequest(orderNumber, J3Code));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3ReconcileAdminResultDto>(JsonOptions);
        body!.AlreadyReconciled.Should().BeTrue();
        body.J3OrderId.Should().Be(RemoteOrderId);
        lookup.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Created_OtherIdOrCode_Refused()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        string orderNumber;
        FakeJ3OrderLookupClient lookup;
        using (var scope = factory.Services.CreateScope())
        {
            lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            (orderId, orderNumber, _) = await SeedAsync(
                scope,
                fulfillmentStatus: J3FulfillmentStatus.Created,
                lastErrorCode: null,
                j3OrderId: "other-id",
                j3OrderCode: "J3OTHER",
                tracking: "J3OTHER");
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-reconcile",
            new J3ReconcileConfirmRequest(orderNumber, J3Code));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3ReconcileErrorCodes.CodeMismatch);
        lookup.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LookupNotFound_FailClosed()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        Guid orderId;
        Guid fulfillmentId;
        string orderNumber;
        using (var scope = factory.Services.CreateScope())
        {
            var lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
            lookup.Reset();
            lookup.NextResult = J3OrderLookupResult.NotFound();
            (orderId, orderNumber, fulfillmentId) = await SeedAsync(scope);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-reconcile",
            new J3ReconcileConfirmRequest(orderNumber, J3Code));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3ReconcileErrorCodes.NotFound);
        await AssertStillUnknownAsync(factory, fulfillmentId);
    }

    [Fact]
    public async Task Anonymous_Returns401()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/j3-reconcile",
            new J3ReconcileConfirmRequest("ES1", J3Code));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ZeroMutations_And_NoSecretsInBody()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        using var scope = factory.Services.CreateScope();
        var createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
        var lookup = scope.ServiceProvider.GetRequiredService<FakeJ3OrderLookupClient>();
        createFake.Reset();
        importFake.Reset();
        lookup.Reset();
        lookup.NextResult = FoundResult();
        var (orderId, orderNumber, _) = await SeedAsync(scope);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-reconcile",
            new J3ReconcileConfirmRequest(orderNumber, J3Code));
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("accessToken");
        body.Should().NotContain("password");
        createFake.CreateCallCount.Should().Be(0);
        importFake.CallCount.Should().Be(0);
    }

    private static J3OrderLookupResult FoundResult() =>
        new()
        {
            Outcome = J3OrderLookupOutcome.Found,
            Response = DeserializeReal()
        };

    private static J3SearchOrderByCodeResponseDto DeserializeReal()
    {
        using var doc = JsonDocument.Parse(RealJson);
        var node = doc.RootElement.GetProperty("data").GetProperty("searchOrderByCode");
        return JsonSerializer.Deserialize<J3SearchOrderByCodeResponseDto>(node.GetRawText(), JsonOptions)!;
    }

    private static Order SampleOrder(string cep = "03065000") => new()
    {
        CustomerName = "Pedro",
        ShipCep = cep,
        Subtotal = 54.9m,
        Discount = 0,
        ShippingPrice = 12.99m
    };

    private static async Task AssertStillUnknownAsync(CustomWebApplicationFactory factory, Guid fulfillmentId)
    {
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<EsoteraDbContext>()
            .J3Fulfillments.SingleAsync(f => f.Id == fulfillmentId);
        row.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        row.J3OrderId.Should().BeNull();
        row.LastErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlAmbiguous);
    }

    private static async Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedAsync(
        IServiceScope scope,
        string fulfillmentStatus = J3FulfillmentStatus.UnknownOutcome,
        string? lastErrorCode = J3FulfillmentErrorCodes.GraphqlAmbiguous,
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
            LastErrorCode = lastErrorCode,
            LastErrorAtUtc = lastErrorCode is null ? null : now,
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

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
