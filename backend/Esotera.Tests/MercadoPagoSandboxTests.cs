using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class MercadoPagoSandboxTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public MercadoPagoSandboxTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PublicConfig_ReportsTestEnvironment_AndSandboxEnabled()
    {
        var res = await _client.GetAsync("/api/payments/config");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var cfg = await res.Content.ReadFromJsonAsync<PaymentEnvironmentConfigDto>(JsonOptions);
        cfg!.Environment.Should().Be("Test");
        cfg.SandboxPixEnabled.Should().BeTrue();
        cfg.SandboxPixAmount.Should().Be(50.00m);
    }

    [Fact]
    public async Task SandboxPixTest_UsesOfficialPayerAndAmount_DoesNotCreateCommercialOrder()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"sbx{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/sandbox/pix-test");
        req.Headers.TryAddWithoutValidation("Idempotency-Key", $"sbx-{Guid.NewGuid():N}"[..32]);
        req.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<SandboxPixTestResponse>(JsonOptions);
        body!.IsSandboxTest.Should().BeTrue();
        body.Amount.Should().Be(50.00m);
        body.ExternalReference.Should().StartWith(MercadoPagoOptions.SandboxExternalReferencePrefix);
        body.QrCode.Should().NotBeNullOrWhiteSpace();
        body.Message.Should().Contain("Ambiente de teste");

        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        fake.Created.Should().NotBeEmpty();
        var cmd = fake.Created[^1];
        cmd.PayerEmail.Should().Be(MercadoPagoOptions.SandboxPayerEmail);
        cmd.PayerFirstName.Should().Be(MercadoPagoOptions.SandboxPayerFirstName);
        cmd.TransactionAmount.Should().Be(50.00m);
        cmd.IsSandboxOfficialTest.Should().BeTrue();
        cmd.PayerCpf.Should().BeNull();
    }

    [Fact]
    public async Task CommercialCheckout_NonFiftyAmount_InTest_IsBlockedWithClearMessage()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"sbxord{Guid.NewGuid():N}@test.com");
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
        order!.Total.Should().NotBe(50.00m);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(null, "pix", null, null, null))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;

        var payRes = await _client.SendAsync(payReq);
        payRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await payRes.Content.ReadAsStringAsync();
        text.Should().Contain("ambiente de teste");
    }

    [Fact]
    public async Task CommercialCheckout_WhenAllowed_UsesSandboxPayer_NotCustomerEmail()
    {
        // Força um pedido cujo total seja 50 via fake: criamos order normal e
        // ajustamos o total no banco in-memory para 50 sem alterar a API comercial.
        var (token, userId) = await TestHelpers.RegisterNewUserAsync(
            _client, $"sbx50{Guid.NewGuid():N}@cliente-real.com");
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

        await TestHelpers.ForceOrderTotalAsync(_factory.Services, order!.Id, 50.00m);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(
                null, "pix", null, null, "cliente-real@gmail.com"))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = _client.DefaultRequestHeaders.Authorization;
        var payRes = await _client.SendAsync(payReq);
        payRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            var cmd = fake.Created[^1];
            cmd.PayerEmail.Should().Be(MercadoPagoOptions.SandboxPayerEmail);
            cmd.PayerFirstName.Should().Be(MercadoPagoOptions.SandboxPayerFirstName);
            cmd.TransactionAmount.Should().Be(50.00m);
            cmd.IsSandboxOfficialTest.Should().BeFalse();
        }

        // Total do pedido comercial permanece 50 (não foi "trocado" por outro valor).
        var get = await _client.GetAsync($"/api/orders/{order.Id}");
        var again = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        again!.Total.Should().Be(50.00m);
        _ = userId;
    }

    [Fact]
    public async Task SandboxEndpoint_Blocked_InProductionEnvironment()
    {
        await using var prodFactory = new ProductionMercadoPagoWebApplicationFactory();
        var client = prodFactory.CreateClient();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"prodblock{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/sandbox/pix-test");
        req.Headers.TryAddWithoutValidation("Idempotency-Key", $"sbx-{Guid.NewGuid():N}"[..32]);
        req.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Production_UsesRealPayer_NotSandboxCredentials()
    {
        await using var prodFactory = new ProductionMercadoPagoWebApplicationFactory();
        var client = prodFactory.CreateClient();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"prodreal{Guid.NewGuid():N}@cliente.com");
        TestHelpers.SetBearerToken(client, token);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);
        var orderRes = await TestHelpers.PostOrderAsync(client, orderReq);
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order!.Id}/payments")
        {
            Content = JsonContent.Create(new CreatePaymentRequest(
                null, "pix", null, null, "cliente-producao@exemplo.com"))
        };
        payReq.Headers.TryAddWithoutValidation("Idempotency-Key", $"pay-{Guid.NewGuid():N}"[..32]);
        payReq.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var payRes = await client.SendAsync(payReq);
        payRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = prodFactory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        var cmd = fake.Created[^1];
        cmd.PayerEmail.Should().Be("cliente-producao@exemplo.com");
        cmd.PayerEmail.Should().NotBe(MercadoPagoOptions.SandboxPayerEmail);
        cmd.PayerFirstName.Should().NotBe(MercadoPagoOptions.SandboxPayerFirstName);
        cmd.TransactionAmount.Should().Be(order.Total);
        cmd.IsSandboxOfficialTest.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_IgnoresSandboxTestOrder_AndDoesNotTouchCommercialOrder()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"whsbx{Guid.NewGuid():N}@test.com");
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
        orderRes.EnsureSuccessStatusCode();
        var order = await orderRes.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        var testOrderId = "ORDTESTSANDBOX00000001";
        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.Seed(new MercadoPagoPaymentSnapshot(
                testOrderId,
                "PAYTEST001",
                "processed",
                "accredited",
                50.00m,
                "BRL",
                $"{MercadoPagoOptions.SandboxExternalReferencePrefix}abc",
                "pix",
                null,
                null,
                null,
                null));
        }

        await PostSignedWebhookAsync(testOrderId, "order.processed");

        var get = await _client.GetAsync($"/api/orders/{order!.Id}");
        var after = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        after!.Status.Should().Be("awaiting_payment");
    }

    [Fact]
    public async Task Webhook_MissingOrder_IsIgnored_WithoutFailing()
    {
        var missingId = "ORDDOESNOTEXIST000001";
        var res = await PostSignedWebhookAsync(missingId, "order.processed");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_Repeated_IsIdempotent()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"whrep{Guid.NewGuid():N}@test.com");
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
        orderRes.EnsureSuccessStatusCode();
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

        (await PostSignedWebhookAsync(payment!.MercadoPagoOrderId!, "order.processed"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostSignedWebhookAsync(payment.MercadoPagoOrderId!, "order.processed"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync($"/api/orders/{order.Id}");
        var after = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        after!.Status.Should().Be("payment_approved");
    }

    private async Task<HttpResponseMessage> PostSignedWebhookAsync(string dataId, string action)
    {
        var body = JsonSerializer.Serialize(new
        {
            action,
            type = "order",
            data = new { id = dataId }
        });
        var secret = "test-webhook-secret";
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
        return await _client.SendAsync(hook);
    }
}

/// <summary>Factory com Mercado Pago em Production (sandbox desligado).</summary>
file class ProductionMercadoPagoWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"EsoteraProdMp_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MERCADO_PAGO_ACCESS_TOKEN"] = "test-access-token-for-unit-tests-only",
                ["MERCADO_PAGO_ENVIRONMENT"] = "Production",
                ["MercadoPago__Environment"] = "Production",
                ["MERCADO_PAGO_SANDBOX_PIX_ENABLED"] = "true",
                ["MERCADO_PAGO_WEBHOOK_SECRET"] = "test-webhook-secret",
                ["PUBLIC_API_BASE_URL"] = "http://localhost",
                ["MELHOR_ENVIO_ENABLED"] = "true",
                ["MELHOR_ENVIO_ENVIRONMENT"] = "sandbox",
                ["MELHOR_ENVIO_CLIENT_ID"] = "100001",
                ["MELHOR_ENVIO_CLIENT_SECRET"] = "test-me-client-secret-not-real",
                ["MELHOR_ENVIO_REDIRECT_URI"] = "http://localhost/api/integrations/melhor-envio/callback",
                ["MELHOR_ENVIO_USER_AGENT"] = "Esotera Test (test@esotera.demo)",
                ["FRONTEND_BASE_URL"] = "https://esotera.vercel.app",
                ["INTEGRATIONS_ENCRYPTION_KEY"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<EsoteraDbContext>) ||
                    d.ServiceType == typeof(EsoteraDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() ==
                     typeof(Microsoft.EntityFrameworkCore.DbContextOptions<>)))
                .ToList();
            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<EsoteraDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(
        Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        db.Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<CatalogBootstrap>().RunAsync().GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<DevSeed>().SeedAsync().GetAwaiter().GetResult();
        ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(host.Services, enabled: true, withOAuthConnection: true)
            .GetAwaiter()
            .GetResult();
        return host;
    }
}
