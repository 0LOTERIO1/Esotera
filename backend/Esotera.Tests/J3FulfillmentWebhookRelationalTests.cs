using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// Autoritativo: webhook MP → payment_approved → EnsurePending no MESMO request (SQLite relacional).
/// EF InMemory não é prova deste fluxo. Zero HTTP J3. Processor não é ligado ao webhook.
/// Invariante: payment_approved AND ShippingMethodId == "j3" → exatamente um J3Fulfillment.
/// J3_FULFILLMENT_ENABLED não participa da criação da obrigação.
/// </summary>
[Collection("sqlite-j3-webhook")]
public class J3FulfillmentWebhookRelationalTests
{
    private readonly SqliteWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    public J3FulfillmentWebhookRelationalTests(SqliteWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void FulfillmentFlag_IsFalse_AndHostUsesFakes_NoProcessorOnPaymentService()
    {
        using var scope = _factory.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.FulfillmentEnabled.Should().BeFalse();
        opts.Enabled.Should().BeTrue();

        scope.ServiceProvider.GetRequiredService<IJ3Client>().Should().BeOfType<FakeJ3Client>();
        scope.ServiceProvider.GetRequiredService<IJ3FulfillmentClient>()
            .Should().BeOfType<FakeJ3FulfillmentClient>();
        scope.ServiceProvider.GetService<J3Client>().Should().BeNull();
        scope.ServiceProvider.GetService<J3FulfillmentHttpClient>().Should().BeNull();

        var paymentCtor = typeof(PaymentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();
        paymentCtor.Should().Contain(typeof(IJ3FulfillmentService));
        paymentCtor.Should().NotContain(typeof(IJ3FulfillmentProcessor));
        paymentCtor.Should().NotContain(typeof(IJ3FulfillmentClient));

        scope.ServiceProvider.GetServices<IHostedService>()
            .Select(s => s.GetType().Name)
            .Should().NotContain(n => n.Contains("J3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WebhookApprovedJ3_SameRequest_CreatesExactlyOnePending_NoManualEnsurePending()
    {
        var fake = ResetFakes();
        var orderId = await CreateJ3OrderAsync();
        await ApproveViaWebhookAsync(orderId, sendTwice: false);

        // Novo scope SOMENTE para verificação — sem EnsurePending.
        await AssertExactlyOnePendingAsync(orderId);
        fake.Mut.CreateCallCount.Should().Be(0);
        fake.Read.Should().BeOfType<FakeJ3Client>();

        using (var verify = _factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var histories = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == orderId)
                .ToListAsync();
            histories.Should().Contain(h => h.ToStatus == OrderStatus.PaymentApproved);
        }
    }

    [Fact]
    public async Task DuplicateWebhook_RemainsExactlyOnePending()
    {
        var fake = ResetFakes();
        var orderId = await CreateJ3OrderAsync();
        await ApproveViaWebhookAsync(orderId, sendTwice: true);

        await AssertExactlyOnePendingAsync(orderId);
        fake.Mut.CreateCallCount.Should().Be(0);

        using (var verify = _factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var approvedHist = await db.OrderStatusHistories.AsNoTracking()
                .CountAsync(h => h.OrderId == orderId && h.ToStatus == OrderStatus.PaymentApproved);
            approvedHist.Should().Be(1);
        }
    }

    [Fact]
    public async Task DuplicateEnsurePendingAfterWebhook_RemainsExactlyOne()
    {
        var orderId = await CreateJ3OrderAsync();
        await ApproveViaWebhookAsync(orderId, sendTwice: false);

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentService>();
            await svc.EnsurePendingAsync(orderId);
            await svc.EnsurePendingAsync(orderId);
        }

        await AssertExactlyOnePendingAsync(orderId);
    }

    [Fact]
    public async Task PacApproved_NoJ3Fulfillment()
    {
        var fake = ResetFakes();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"whpac{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var req = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            ShippingMethod.MelhorEconomico,
            "pix",
            null,
            null);
        var create = await TestHelpers.PostOrderAsync(_client, req);
        create.EnsureSuccessStatusCode();
        var order = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        await ApproveViaWebhookAsync(order!.Id, sendTwice: false, customerToken: token);

        await AssertOrderApprovedAsync(order.Id);
        await AssertNoFulfillmentAsync(order.Id);
        fake.Mut.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task J3AwaitingPayment_NoFulfillment()
    {
        var fake = ResetFakes();
        var orderId = await CreateJ3OrderAsync();

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.AwaitingPayment);
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
        fake.Mut.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task J3PixCreated_PaymentPending_NoFulfillment()
    {
        var fake = ResetFakes();
        var (token, orderId) = await CreateJ3OrderWithTokenAsync();
        await TestHelpers.ForceOrderTotalAsync(_factory.Services, orderId, 50.00m);
        TestHelpers.SetBearerToken(_client, token);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(null, "pix", null, null, null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
        var payRes = await _client.SendAsync(payReq);
        payRes.EnsureSuccessStatusCode();

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.AwaitingPayment);
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
        fake.Mut.CreateCallCount.Should().Be(0);
    }

    private (FakeJ3FulfillmentClient Mut, FakeJ3Client Read) ResetFakes()
    {
        using var scope = _factory.Services.CreateScope();
        var mut = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var read = scope.ServiceProvider.GetRequiredService<FakeJ3Client>();
        mut.Reset();
        return (mut, read);
    }

    private async Task AssertExactlyOnePendingAsync(Guid orderId)
    {
        await AssertOrderApprovedAsync(orderId);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var rows = await db.J3Fulfillments.AsNoTracking()
            .Where(f => f.OrderId == orderId)
            .ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Status.Should().Be(J3FulfillmentStatus.Pending);
        rows[0].AttemptCount.Should().Be(0);
        rows[0].OrderId.Should().Be(orderId);
    }

    private async Task AssertOrderApprovedAsync(Guid orderId)
    {
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.PaymentApproved);
    }

    private async Task AssertNoFulfillmentAsync(Guid orderId)
    {
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
    }

    private async Task<Guid> CreateJ3OrderAsync()
    {
        var (_, orderId) = await CreateJ3OrderWithTokenAsync();
        return orderId;
    }

    private async Task<(string Token, Guid OrderId)> CreateJ3OrderWithTokenAsync()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"whj3{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var create = await TestHelpers.PostOrderAsync(_client, J3OrderRequest());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        return (token, order!.Id);
    }

    private async Task ApproveViaWebhookAsync(Guid orderId, bool sendTwice, string? customerToken = null)
    {
        if (customerToken is not null)
            TestHelpers.SetBearerToken(_client, customerToken);

        await TestHelpers.ForceOrderTotalAsync(_factory.Services, orderId, 50.00m);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(null, "pix", null, null, null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
        var payRes = await _client.SendAsync(payReq);
        payRes.EnsureSuccessStatusCode();
        var payment = await payRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);

        using (var scope = _factory.Services.CreateScope())
        {
            var mp = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            mp.SetStatus(
                payment!.MercadoPagoOrderId!,
                "processed",
                50.00m,
                orderId.ToString("D"),
                payment.MercadoPagoPaymentId,
                "accredited");
        }

        await SendWebhookAsync(payment!.MercadoPagoOrderId!);
        if (sendTwice)
            await SendWebhookAsync(payment.MercadoPagoOrderId!);
    }

    private async Task SendWebhookAsync(string dataId)
    {
        var body = JsonSerializer.Serialize(new
        {
            action = "order.processed",
            type = "order",
            data = new { id = dataId }
        });
        const string secret = "test-webhook-secret";
        var requestId = Guid.NewGuid().ToString("N");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest));
        var v1 = Convert.ToHexString(hash).ToLowerInvariant();

        using var hook = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        hook.Headers.TryAddWithoutValidation("x-signature", $"ts={ts},v1={v1}");
        hook.Headers.TryAddWithoutValidation("x-request-id", requestId);
        var hookRes = await _client.SendAsync(hook);
        hookRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static CreateOrderRequest J3OrderRequest() =>
        new(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: true),
            null,
            ShippingMethod.J3,
            "pix",
            null,
            null);
}
