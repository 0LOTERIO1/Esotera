using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class MercadoPagoPaymentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public MercadoPagoPaymentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_StartsAsAwaitingPayment_ThenPixPaymentReturnsQr()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"paypix{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var orderRes = await TestHelpers.PostOrderAsync(_client, orderReq);
        orderRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Status.Should().Be("awaiting_payment");

        // Em Test, checkout comercial só aceita o valor oficial de sandbox (não altera a API).
        await TestHelpers.ForceOrderTotalAsync(_factory.Services, order.Id, 50.00m);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(
                null,
                "pix",
                null,
                null,
                null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;

        var payRes = await _client.SendAsync(payReq);
        payRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await payRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        payment.Should().NotBeNull();
        payment!.QrCode.Should().NotBeNullOrWhiteSpace();
        payment.QrCodeBase64.Should().NotBeNullOrWhiteSpace();
        payment.Status.Should().Be("pending");
        payment.MercadoPagoOrderId.Should().StartWith("ORD");
        payment.MercadoPagoPaymentId.Should().StartWith("PAY");
        payment.Message.Should().Contain("Aguardando pagamento");
        payment.Amount.Should().Be(50.00m);
    }

    [Fact]
    public async Task CreatePayment_Card_IsRejectedInPhase1()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"paycard{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);
        var orderRes = await TestHelpers.PostOrderAsync(_client, orderReq);
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order!.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(
                "fake-card-token",
                "visa",
                1,
                null,
                null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;

        var payRes = await _client.SendAsync(payReq);
        payRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void CreatePayment_CardToken_DoesNotRequirePanOrCvv()
    {
        var props = typeof(CreatePaymentRequest).GetProperties().Select(p => p.Name).ToHashSet();
        props.Should().NotContain("CardNumber");
        props.Should().NotContain("Cvv");
        props.Should().NotContain("SecurityCode");
        props.Should().Contain("Token");
    }

    [Fact]
    public async Task Webhook_OrderProcessed_MarksOrderPaid_Idempotently()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"payhook{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "j3",
            "pix",
            null,
            null);
        var orderRes = await TestHelpers.PostOrderAsync(_client, orderReq);
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        await TestHelpers.ForceOrderTotalAsync(_factory.Services, order!.Id, 50.00m);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(null, "pix", null, null, null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
        var payRes = await _client.SendAsync(payReq);
        var payment = await payRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.SetStatus(
                payment!.MercadoPagoOrderId!,
                "processed",
                50.00m,
                order.Id.ToString("D"),
                payment.MercadoPagoPaymentId,
                "accredited");
        }

        var body = JsonSerializer.Serialize(new
        {
            action = "order.processed",
            type = "order",
            data = new { id = payment!.MercadoPagoOrderId }
        });

        var secret = "test-webhook-secret";
        var dataId = payment.MercadoPagoOrderId!;
        var requestId = Guid.NewGuid().ToString("N");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{ts};";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(manifest));
        var v1 = Convert.ToHexString(hash).ToLowerInvariant();

        using var hook1 = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        hook1.Headers.TryAddWithoutValidation("x-signature", $"ts={ts},v1={v1}");
        hook1.Headers.TryAddWithoutValidation("x-request-id", requestId);

        var hookRes1 = await _client.SendAsync(hook1);
        hookRes1.StatusCode.Should().Be(HttpStatusCode.OK);

        TestHelpers.SetBearerToken(_client, token);
        var get1 = await _client.GetAsync($"/api/orders/{order.Id}");
        var after1 = await get1.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        after1!.Status.Should().Be("payment_approved");

        using var hook2 = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        hook2.Headers.TryAddWithoutValidation("x-signature", $"ts={ts},v1={v1}");
        hook2.Headers.TryAddWithoutValidation("x-request-id", requestId);
        (await _client.SendAsync(hook2)).StatusCode.Should().Be(HttpStatusCode.OK);

        var get2 = await _client.GetAsync($"/api/orders/{order.Id}");
        var after2 = await get2.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        after2!.Status.Should().Be("payment_approved");
    }

    [Fact]
    public async Task BrowserReturn_DoesNotMarkPaid_WithoutWebhook()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"payret{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_expresso",
            "pix",
            null,
            null);
        var orderRes = await TestHelpers.PostOrderAsync(_client, orderReq);
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Status.Should().Be("awaiting_payment");

        var get = await _client.GetAsync($"/api/orders/{order.Id}");
        var again = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        again!.Status.Should().Be("awaiting_payment");
    }
}
