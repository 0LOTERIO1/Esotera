using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
using Esotera.Application.Interfaces;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// SQLite: transaction local payment_approved + histórico + Pending.
/// Falha no insert de J3Fulfillment (interceptor) → rollback de Order e History.
/// EF InMemory não é prova. Zero HTTP J3. Processor não entra no webhook.
/// </summary>
[Collection("sqlite-j3-pending-fail")]
public class J3FulfillmentAtomicCommitTests
{
    private readonly SqliteJ3FulfillmentInsertFailsWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    public J3FulfillmentAtomicCommitTests(SqliteJ3FulfillmentInsertFailsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EnsurePendingInsertFails_RollsBackOrderHistoryAndFulfillment_HttpStill200()
    {
        var fake = ResetFakes();
        var (token, orderId) = await CreateJ3OrderWithTokenAsync();

        int historyBefore;
        string statusBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            statusBefore = order.Status;
            historyBefore = await db.OrderStatusHistories.CountAsync(h => h.OrderId == orderId);
        }

        statusBefore.Should().Be(OrderStatus.AwaitingPayment);

        var hookRes = await ApproveViaWebhookAsync(orderId, token);
        hookRes.Should().Be(HttpStatusCode.OK);

        using (var verify = _factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
            order.Status.Should().Be(OrderStatus.AwaitingPayment);
            order.Status.Should().NotBe(OrderStatus.PaymentApproved);

            var historyAfter = await db.OrderStatusHistories.AsNoTracking()
                .Where(h => h.OrderId == orderId)
                .ToListAsync();
            historyAfter.Should().HaveCount(historyBefore);
            historyAfter.Should().NotContain(h => h.ToStatus == OrderStatus.PaymentApproved);

            (await db.J3Fulfillments.CountAsync(f => f.OrderId == orderId)).Should().Be(0);
        }

        fake.Mut.CreateCallCount.Should().Be(0);
        fake.Read.Should().BeOfType<FakeJ3Client>();
    }

    [Fact]
    public async Task PacApproved_InterceptorDoesNotFire_NoJ3Fulfillment()
    {
        var fake = ResetFakes();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"atompac{Guid.NewGuid():N}@test.com");
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

        var hookRes = await ApproveViaWebhookAsync(order!.Id, token);
        hookRes.Should().Be(HttpStatusCode.OK);

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var paid = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        paid.Status.Should().Be(OrderStatus.PaymentApproved);
        (await db.J3Fulfillments.CountAsync(f => f.OrderId == order.Id)).Should().Be(0);
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

    private async Task<(string Token, Guid OrderId)> CreateJ3OrderWithTokenAsync()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"atomj3{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var create = await TestHelpers.PostOrderAsync(_client, new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: true),
            null,
            ShippingMethod.J3,
            "pix",
            null,
            null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        return (token, order!.Id);
    }

    private async Task<HttpStatusCode> ApproveViaWebhookAsync(Guid orderId, string customerToken)
    {
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

        var dataId = payment!.MercadoPagoOrderId!;
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
        return hookRes.StatusCode;
    }
}
