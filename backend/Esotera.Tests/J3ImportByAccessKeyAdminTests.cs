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
/// POST Admin j3-import-by-access-key. Zero mutation real J3 / zero Production write.
/// </summary>
public class J3ImportByAccessKeyAdminTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task FlagOff_Returns409_ZeroHttp()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        using var scope = factory.Services.CreateScope();
        var importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
        var createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        importFake.Reset();
        createFake.Reset();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var (orderId, orderNumber) = await SeedRecoveryOrderAsync(db, scope.ServiceProvider);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentErrorCodes.ImportByAccessKeyDisabled);
        importFake.CallCount.Should().Be(0);
        createFake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task FulfillmentEnabled_BlocksRecovery_ZeroHttp()
    {
        using var factory = new J3ImportBlockedByFulfillmentWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        FakeJ3FulfillmentClient createFake;
        Guid orderId;
        string orderNumber;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake.Reset();
            createFake.Reset();
            (orderId, orderNumber) = await SeedRecoveryOrderAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentErrorCodes.FulfillmentMustBeDisabled);
        importFake.CallCount.Should().Be(0);
        createFake.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WrongConfirmOrderNumber_Returns400()
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        Guid orderId;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            importFake.Reset();
            (orderId, _) = await SeedRecoveryOrderAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest("ES-WRONG-NUMBER"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        importFake.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task WrongShipping_Conflict()
    {
        await RunBlockedAsync(
            mutateOrder: o => o.ShippingMethodId = ShippingMethod.MelhorExpresso,
            expectedReason: J3FulfillmentEligibilityCodes.WrongShippingMethod);
    }

    [Fact]
    public async Task PaymentNotApproved_Conflict()
    {
        await RunBlockedAsync(
            mutateOrder: o => o.Status = OrderStatus.AwaitingPayment,
            expectedReason: J3FulfillmentEligibilityCodes.PaymentNotApproved);
    }

    [Fact]
    public async Task MissingFiscal_Conflict()
    {
        await RunBlockedAsync(
            withFiscal: false,
            expectedReason: J3FulfillmentEligibilityCodes.MissingFiscalInvoice);
    }

    [Fact]
    public async Task FiscalNotAuthorized_Conflict()
    {
        await RunBlockedAsync(
            fiscalStatus: FiscalInvoiceStatus.Unknown,
            expectedReason: J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized);
    }

    [Fact]
    public async Task FulfillmentNotUnknownOutcome_Conflict()
    {
        await RunBlockedAsync(
            fulfillmentStatus: J3FulfillmentStatus.Pending,
            lastErrorCode: J3FulfillmentErrorCodes.GraphqlAmbiguous,
            expectedReason: J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview);
    }

    [Fact]
    public async Task LastErrorCodeNotAmbiguous_Conflict()
    {
        await RunBlockedAsync(
            fulfillmentStatus: J3FulfillmentStatus.UnknownOutcome,
            lastErrorCode: J3FulfillmentErrorCodes.TimeoutUnknown,
            expectedReason: J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview);
    }

    [Fact]
    public async Task Success_Returns200_DoesNotMutateFulfillment_NeverCallsCreateTms()
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        FakeJ3FulfillmentClient createFake;
        Guid orderId;
        string orderNumber;
        Guid fulfillmentId;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake.Reset();
            createFake.Reset();
            importFake.NextResult = J3CreateOrderAttemptResult.Success("imported", null, null, null);
            (orderId, orderNumber, fulfillmentId) = await SeedRecoveryOrderFullAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<J3ImportByAccessKeyAdminResultDto>(JsonOptions);
        body!.Outcome.Should().Be(nameof(J3CreateOrderOutcome.Success));
        body.FulfillmentUnchanged.Should().BeTrue();
        body.HttpSent.Should().BeTrue();
        body.OperationName.Should().Be(J3ImportOrderByAccessKeyMutation.OperationName);
        body.FulfillmentStatus.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        importFake.CallCount.Should().Be(1);
        importFake.LastParsed.Should().NotBeNull();
        importFake.LastParsed!.ChNFe.Should().HaveLength(44);
        createFake.CreateCallCount.Should().Be(0);

        using var verify = factory.Services.CreateScope();
        var row = await verify.ServiceProvider.GetRequiredService<EsoteraDbContext>()
            .J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
        row.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        row.LastErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlAmbiguous);
        row.J3OrderId.Should().BeNull();
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task SuccessFalse_NoRetry_FulfillmentUnchanged()
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        Guid orderId;
        string orderNumber;
        Guid fulfillmentId;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            importFake.Reset();
            importFake.NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.SuccessFalse);
            (orderId, orderNumber, fulfillmentId) = await SeedRecoveryOrderFullAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        importFake.CallCount.Should().Be(1);

        using var verify = factory.Services.CreateScope();
        var row = await verify.ServiceProvider.GetRequiredService<EsoteraDbContext>()
            .J3Fulfillments.AsNoTracking().SingleAsync(f => f.Id == fulfillmentId);
        row.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task GraphqlUnauthenticated_DefiniteFailure_ZeroRetry()
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        Guid orderId;
        string orderNumber;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            importFake.Reset();
            importFake.NextResult =
                J3CreateOrderAttemptResult.DefiniteFailure(J3FulfillmentErrorCodes.GraphqlUnauthenticated);
            (orderId, orderNumber, _) = await SeedRecoveryOrderFullAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(J3FulfillmentErrorCodes.GraphqlUnauthenticated);
        importFake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Ambiguous_Conflict_ZeroRetry()
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        Guid orderId;
        string orderNumber;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            importFake.Reset();
            importFake.NextResult =
                J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.GraphqlAmbiguous);
            (orderId, orderNumber, _) = await SeedRecoveryOrderFullAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        importFake.CallCount.Should().Be(1);
    }

    [Fact]
    public void Controller_TakesImportAdminService_NotHttpClients()
    {
        var types = typeof(Esotera.Api.Controllers.AdminOrdersController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToArray();
        types.Should().Contain(typeof(IJ3ImportOrderByAccessKeyAdminService));
        types.Should().NotContain(typeof(IJ3ImportOrderByAccessKeyClient));
        types.Should().NotContain(typeof(IJ3FulfillmentClient));
    }

    private static async Task RunBlockedAsync(
        Action<Order>? mutateOrder = null,
        bool withFiscal = true,
        string fiscalStatus = FiscalInvoiceStatus.Authorized,
        string fulfillmentStatus = J3FulfillmentStatus.UnknownOutcome,
        string? lastErrorCode = J3FulfillmentErrorCodes.GraphqlAmbiguous,
        string expectedReason = "")
    {
        using var factory = new J3ImportByAccessKeyRecoveryWebApplicationFactory();
        var client = factory.CreateClient();
        await SetAdminAsync(client);

        FakeJ3ImportOrderByAccessKeyClient importFake;
        FakeJ3FulfillmentClient createFake;
        Guid orderId;
        string orderNumber;
        using (var scope = factory.Services.CreateScope())
        {
            importFake = scope.ServiceProvider.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
            createFake = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
            importFake.Reset();
            createFake.Reset();
            (orderId, orderNumber, _) = await SeedRecoveryOrderFullAsync(
                scope.ServiceProvider.GetRequiredService<EsoteraDbContext>(),
                scope.ServiceProvider,
                withFiscal,
                fiscalStatus,
                fulfillmentStatus,
                lastErrorCode,
                mutateOrder);
        }

        var response = await client.PostAsJsonAsync(
            $"/api/admin/orders/{orderId}/j3-import-by-access-key",
            new J3ImportByAccessKeyConfirmRequest(orderNumber));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        GetReason(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .Should().Be(expectedReason);
        importFake.CallCount.Should().Be(0);
        createFake.CreateCallCount.Should().Be(0);
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

    private static async Task<(Guid OrderId, string OrderNumber)> SeedRecoveryOrderAsync(
        EsoteraDbContext db,
        IServiceProvider sp)
    {
        var full = await SeedRecoveryOrderFullAsync(db, sp);
        return (full.OrderId, full.OrderNumber);
    }

    private static async Task<(Guid OrderId, string OrderNumber, Guid FulfillmentId)> SeedRecoveryOrderFullAsync(
        EsoteraDbContext db,
        IServiceProvider sp,
        bool withFiscal = true,
        string fiscalStatus = FiscalInvoiceStatus.Authorized,
        string fulfillmentStatus = J3FulfillmentStatus.UnknownOutcome,
        string? lastErrorCode = J3FulfillmentErrorCodes.GraphqlAmbiguous,
        Action<Order>? mutate = null)
    {
        var enc = sp.GetRequiredService<IIntegrationsEncryptionService>();
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
            ShipStreet = "Rua Dest",
            ShipNumber = "200",
            ShipNeighborhood = "Bairro",
            ShipCity = "São Paulo",
            ShipState = "SP",
            ShippingIsResidentialAddress = true,
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Cliente Teste",
            CustomerEmail = user.Email,
            CustomerPhone = "11988887777",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        mutate?.Invoke(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        if (withFiscal)
        {
            var chNFe = NewSyntheticChNFe();
            var xml = FiscalInvoiceImportTests.BuildSyntheticAuthorizedXml(chNFe: chNFe);
            db.FiscalInvoices.Add(new FiscalInvoice
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = fiscalStatus,
                ChNFe = chNFe,
                Number = "3",
                Series = "9",
                AuthorizedAtUtc = fiscalStatus == FiscalInvoiceStatus.Authorized ? now : null,
                XmlCipher = enc.Encrypt(xml),
                XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
                Source = FiscalInvoiceSource.ManualUpload,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var fulfillmentId = Guid.NewGuid();
        db.J3Fulfillments.Add(new J3Fulfillment
        {
            Id = fulfillmentId,
            OrderId = order.Id,
            Status = fulfillmentStatus,
            AttemptCount = 1,
            LastErrorCode = lastErrorCode,
            LastErrorAtUtc = now,
            StartedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        return (order.Id, order.OrderNumber, fulfillmentId);
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
