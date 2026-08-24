using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
using Esotera.Application.Validators;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class MercadoPagoAttemptHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public MercadoPagoAttemptHardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task StaleWebhook_AfterBApproved_IgnoresAttemptA_KeepsB()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        string attemptAId;
        string attemptAPayId;

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "rejected";
            fake.NextCreateStatusDetail = "cc_rejected_other_reason";
        }

        var aRes = await PostPaymentAsync(
            order.Id,
            auth,
            CardReq("tok_a"),
            "idem-hard-a-aaaaaaa");
        var aBody = await aRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        aBody!.Status.Should().Be("rejected");
        attemptAId = aBody.MercadoPagoOrderId!;
        attemptAPayId = aBody.MercadoPagoPaymentId!;

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "processed";
            fake.NextCreateStatusDetail = "accredited";
        }

        const string keyB = "idem-hard-b-bbbbbbb";
        var bRes = await PostPaymentAsync(order.Id, auth, CardReq("tok_b"), keyB);
        var bBody = await bRes.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        bBody!.Status.Should().Be("approved");
        var attemptBId = bBody.MercadoPagoOrderId!;
        var attemptBPayId = bBody.MercadoPagoPaymentId!;
        attemptBId.Should().NotBe(attemptAId);

        // Webhook atrasado de A (ainda seeded no fake com rejected).
        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.SetStatus(
                attemptAId,
                "rejected",
                50.00m,
                order.Id.ToString("D"),
                attemptAPayId,
                "cc_rejected_other_reason");
        }

        await PostSignedWebhookAsync(attemptAId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            entity.Status.Should().Be(OrderStatus.PaymentApproved);
            entity.MercadoPagoOrderId.Should().Be(attemptBId);
            entity.MercadoPagoPaymentId.Should().Be(attemptBPayId);
            entity.PaymentIdempotencyKey.Should().Be(keyB);
            entity.PaymentStatus.Should().Be("approved");
        }

        (await GetOrderAsync(order.Id, auth)).Status.Should().Be("payment_approved");
    }

    [Fact]
    public async Task StaleWebhook_WhileBPending_KeepsBAuthoritative()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "rejected";
            fake.NextCreateStatusDetail = "cc_rejected_other_reason";
        }

        var aBody = await (await PostPaymentAsync(order.Id, auth, CardReq("tok_a"), "idem-pend-a-aaaaaa"))
            .Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        var attemptAId = aBody!.MercadoPagoOrderId!;

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "in_process";
            fake.NextCreateStatusDetail = "pending_contingency";
        }

        const string keyB = "idem-pend-b-bbbbbb";
        var bBody = await (await PostPaymentAsync(order.Id, auth, CardReq("tok_b"), keyB))
            .Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        bBody!.Status.Should().Be("pending");
        var attemptBId = bBody.MercadoPagoOrderId!;

        await PostSignedWebhookAsync(attemptAId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            entity.Status.Should().Be(OrderStatus.AwaitingPayment);
            entity.MercadoPagoOrderId.Should().Be(attemptBId);
            entity.PaymentIdempotencyKey.Should().Be(keyB);
            entity.PaymentStatus.Should().Be("pending");
        }
    }

    [Theory]
    [InlineData("in_process", "pending_contingency")]
    [InlineData("rejected", "cc_rejected_other_reason")]
    [InlineData("failed", "failed")]
    public async Task Approved_Monotonic_IgnoresNonReversalWebhook(string status, string detail)
    {
        var (order, auth, mpOrderId, key) = await CreateApprovedCardAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.SetStatus(mpOrderId, status, 50.00m, order.Id.ToString("D"), statusDetail: detail);
        }

        await PostSignedWebhookAsync(mpOrderId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            entity.Status.Should().Be(OrderStatus.PaymentApproved);
            entity.PaymentStatus.Should().Be("approved");
            entity.PaymentIdempotencyKey.Should().Be(key);
            entity.MercadoPagoOrderId.Should().Be(mpOrderId);
            PaymentService.CanStartNewPaymentAttempt(entity).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("refunded")]
    [InlineData("charged_back")]
    public async Task Approved_AllowsFinancialReversal_ToCancelled(string reversalStatus)
    {
        var (order, auth, mpOrderId, _) = await CreateApprovedCardAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.SetStatus(mpOrderId, reversalStatus, 50.00m, order.Id.ToString("D"), statusDetail: reversalStatus);
        }

        await PostSignedWebhookAsync(mpOrderId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            entity.Status.Should().Be(OrderStatus.Cancelled);
        }
    }

    [Fact]
    public async Task AmbiguousGetTimeout_DoesNotClearIdempotency_NorAllowNewKey()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "in_process";
            fake.NextCreateStatusDetail = "pending_contingency";
        }

        const string key = "idem-timeout-samekey1";
        var first = await PostPaymentAsync(order.Id, auth, CardReq("tok_t"), key);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await first.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        var createCountBefore = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            createCountBefore = fake.Created.Count(c => c.ExternalReference == order.Id.ToString("D"));
            fake.FailNextGetWithException = new TimeoutException("simulated MP timeout");
        }

        var replay = await PostPaymentAsync(order.Id, auth, CardReq("tok_t"), key);
        replay.IsSuccessStatusCode.Should().BeFalse();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
            entity.PaymentIdempotencyKey.Should().Be(key);
            entity.MercadoPagoOrderId.Should().Be(body!.MercadoPagoOrderId);
            entity.Status.Should().Be(OrderStatus.AwaitingPayment);

            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.Created.Count(c => c.ExternalReference == order.Id.ToString("D"))
                .Should().Be(createCountBefore);
        }

        var otherKey = await PostPaymentAsync(
            order.Id,
            auth,
            CardReq("tok_other"),
            "idem-timeout-otherkey");
        otherKey.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("pix", "credit_card")]
    [InlineData("pix", "debit_card")]
    [InlineData("bolbradesco", "debit_card")]
    [InlineData("boleto", "credit_card")]
    public async Task Validator_RejectsReservedIdWithCardType(string methodId, string type)
    {
        var validator = new CreatePaymentRequestValidator();
        var result = await validator.TestValidateAsync(new CreatePaymentRequest(
            "tok_x",
            methodId,
            type == "credit_card" ? 1 : null,
            null,
            null,
            type));
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethodId);
    }

    [Theory]
    [InlineData("visa", "credit_card")]
    [InlineData("master", "debit_card")]
    public async Task Validator_AcceptsGenericCardIds(string methodId, string type)
    {
        var validator = new CreatePaymentRequestValidator();
        var result = await validator.TestValidateAsync(new CreatePaymentRequest(
            "tok_ok",
            methodId,
            type == "credit_card" ? 1 : null,
            null,
            null,
            type));
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreatePaymentRequest CardReq(string token) =>
        new(token, "visa", 1, null, null, "credit_card");

    private async Task<(OrderDto Order, string Auth, string MpOrderId, string Key)> CreateApprovedCardAsync()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "processed";
            fake.NextCreateStatusDetail = "accredited";
        }

        const string key = "idem-approved-mono01";
        var pay = await PostPaymentAsync(order.Id, auth, CardReq("tok_ok"), key);
        pay.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await pay.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        body!.Status.Should().Be("approved");
        return (order, auth, body.MercadoPagoOrderId!, key);
    }

    private async Task<(OrderDto Order, string Auth)> CreateAwaitingOrderAsync(string paymentMethod)
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"ah{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            paymentMethod,
            paymentMethod == "card" ? 1 : null,
            null);
        var orderRes = await TestHelpers.PostOrderAsync(_client, orderReq);
        orderRes.EnsureSuccessStatusCode();
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        await TestHelpers.ForceOrderTotalAsync(_factory.Services, order!.Id, 50.00m);
        return (order, token);
    }

    private async Task<HttpResponseMessage> PostPaymentAsync(
        Guid orderId,
        string auth,
        CreatePaymentRequest body,
        string idempotencyKey)
    {
        TestHelpers.SetBearerToken(_client, auth);
        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(body)
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
        return await _client.SendAsync(payReq);
    }

    private async Task<OrderDto> GetOrderAsync(Guid orderId, string auth)
    {
        TestHelpers.SetBearerToken(_client, auth);
        var get = await _client.GetAsync($"/api/orders/{orderId}");
        get.EnsureSuccessStatusCode();
        return (await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions))!;
    }

    private async Task PostSignedWebhookAsync(string dataId)
    {
        var body = JsonSerializer.Serialize(new
        {
            action = "order.updated",
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
        var res = await _client.SendAsync(hook);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
