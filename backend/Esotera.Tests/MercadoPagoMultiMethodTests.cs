using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Payments;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

public class MercadoPagoMultiMethodTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public MercadoPagoMultiMethodTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreditCard_Approved_UsesToken_AndMarksPaid()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        fake.NextCreateStatus = "processed";
        fake.NextCreateStatusDetail = "accredited";

        var pay = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            "tok_credit_safe_abc",
            "visa",
            1,
            "25",
            null,
            "credit_card"));

        pay.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await pay.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        body!.Status.Should().Be("approved");
        body.MercadoPagoOrderId.Should().StartWith("ORD");
        body.QrCode.Should().BeNullOrEmpty();

        fake.Created.Should().ContainSingle(c =>
            c.ExternalReference == order.Id.ToString("D"));
        var cmd = fake.Created.Single(c => c.ExternalReference == order.Id.ToString("D"));
        cmd.PaymentMethodType.Should().Be("credit_card");
        cmd.PaymentMethodId.Should().Be("visa");
        cmd.Token.Should().Be("tok_credit_safe_abc");
        cmd.Installments.Should().Be(1);
        cmd.TransactionAmount.Should().Be(50.00m);

        var get = await GetOrderAsync(order.Id, auth);
        get.Status.Should().Be("payment_approved");
    }

    [Fact]
    public async Task CreditCard_Pending_StaysAwaitingPayment()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        fake.NextCreateStatus = "in_process";
        fake.NextCreateStatusDetail = "pending_contingency";

        var pay = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            "tok_pending",
            "master",
            1,
            null,
            null,
            "credit_card"));

        var body = await pay.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        body!.Status.Should().Be("pending");
        (await GetOrderAsync(order.Id, auth)).Status.Should().Be("awaiting_payment");
    }

    [Fact]
    public async Task CreditCard_Rejected_AllowsNewAttempt_WithNewKey()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "rejected";
            fake.NextCreateStatusDetail = "cc_rejected_other_reason";
        }

        var first = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            "tok_bad",
            "visa",
            1,
            null,
            null,
            "credit_card"),
            "idem-reject-aaaaaaa");
        var firstBody = await first.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        firstBody!.Status.Should().Be("rejected");
        (await GetOrderAsync(order.Id, auth)).Status.Should().Be("awaiting_payment");

        using (var scope = _factory.Services.CreateScope())
        {
            var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
            fake.NextCreateStatus = "processed";
            fake.NextCreateStatusDetail = "accredited";
        }

        var second = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            "tok_good",
            "visa",
            1,
            null,
            null,
            "credit_card"),
            "idem-reject-bbbbbbb");
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        secondBody!.Status.Should().Be("approved");
    }

    [Fact]
    public async Task CreditCard_SameIdempotencyKey_IsReplay()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        fake.NextCreateStatus = "in_process";
        fake.NextCreateStatusDetail = "pending_contingency";

        const string key = "idem-same-key-replay01";
        var req = new CreatePaymentRequest("tok_a", "visa", 1, null, null, "credit_card");
        var first = await PostPaymentAsync(order.Id, auth, req, key);
        var firstBody = await first.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);

        var second = await PostPaymentAsync(order.Id, auth, req, key);
        var secondBody = await second.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody!.MercadoPagoOrderId.Should().Be(firstBody!.MercadoPagoOrderId);
        fake.Created.Count(c => c.ExternalReference == order.Id.ToString("D")).Should().Be(1);
    }

    [Fact]
    public async Task PendingAttempt_BlocksDifferentIdempotencyKey()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("pix");
        var first = await PostPaymentAsync(
            order.Id,
            auth,
            new CreatePaymentRequest(null, "pix", null, null, null, "bank_transfer"),
            "idem-pending-aaaaaaa");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await PostPaymentAsync(
            order.Id,
            auth,
            new CreatePaymentRequest("tok", "visa", 1, null, null, "credit_card"),
            "idem-pending-bbbbbbb");
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approved_BlocksNewPaymentAttempt()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();
        fake.NextCreateStatus = "processed";
        fake.NextCreateStatusDetail = "accredited";

        (await PostPaymentAsync(
            order.Id,
            auth,
            new CreatePaymentRequest("tok1", "visa", 1, null, null, "credit_card"),
            "idem-approved-aaaaaa")).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await PostPaymentAsync(
            order.Id,
            auth,
            new CreatePaymentRequest("tok2", "visa", 1, null, null, "credit_card"),
            "idem-approved-bbbbbb");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DebitCard_RequiresToken_PreservesMethodId_NoInstallments()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("card");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();

        var pay = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            "tok_debit",
            "elo",
            null,
            null,
            null,
            "debit_card"));

        pay.StatusCode.Should().Be(HttpStatusCode.OK);
        var cmd = fake.Created.Single(c => c.ExternalReference == order.Id.ToString("D"));
        cmd.PaymentMethodType.Should().Be("debit_card");
        cmd.PaymentMethodId.Should().Be("elo");
        cmd.Token.Should().Be("tok_debit");
        cmd.Installments.Should().BeNull();
    }

    [Fact]
    public async Task Boleto_Bolbradesco_MapsToBoleto_PendingNotApproved()
    {
        var (order, auth) = await CreateAwaitingOrderAsync("boleto");
        using var scope = _factory.Services.CreateScope();
        var fake = scope.ServiceProvider.GetRequiredService<FakeMercadoPagoClient>();

        var pay = await PostPaymentAsync(order.Id, auth, new CreatePaymentRequest(
            null,
            "bolbradesco",
            null,
            null,
            null,
            "ticket"));

        pay.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await pay.Content.ReadFromJsonAsync<CreatePaymentResponse>(JsonOptions);
        body!.Status.Should().Be("pending");
        body.TicketUrl.Should().NotBeNullOrWhiteSpace();
        body.DigitableLine.Should().NotBeNullOrWhiteSpace();
        body.BarcodeContent.Should().NotBeNullOrWhiteSpace();
        body.Message.Should().Contain("Boleto");

        fake.Created.Should().ContainSingle(c => c.ExternalReference == order.Id.ToString("D"));
        fake.Created.Single(c => c.ExternalReference == order.Id.ToString("D"))
            .PaymentMethodId.Should().Be("bolbradesco");
        fake.Created.Single(c => c.ExternalReference == order.Id.ToString("D"))
            .PaymentMethodType.Should().Be("ticket");
        MercadoPagoHttpClient.MapTicketPaymentMethodId("bolbradesco").Should().Be("boleto");

        (await GetOrderAsync(order.Id, auth)).Status.Should().Be("awaiting_payment");
    }

    [Fact]
    public void CreatePaymentRequest_HasNoPanOrCvv()
    {
        var props = typeof(CreatePaymentRequest).GetProperties().Select(p => p.Name).ToHashSet();
        props.Should().NotContain("CardNumber");
        props.Should().NotContain("Cvv");
        props.Should().NotContain("SecurityCode");
        props.Should().NotContain("ExpirationMonth");
        props.Should().Contain("Token");
        props.Should().Contain("PaymentMethodType");
    }

    [Fact]
    public async Task HttpClient_CreditBody_ContainsToken_NotPan_AndRedactsTokenInLogs()
    {
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "ORDCARD00000000000001",
                      "status": "processed",
                      "status_detail": "accredited",
                      "external_reference": "11111111-1111-1111-1111-111111111111",
                      "total_amount": "50.00",
                      "currency_id": "BRL",
                      "transactions": {
                        "payments": [{
                          "id": "PAYCARD00000000000001",
                          "payment_method": { "id": "visa", "type": "credit_card" }
                        }]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var logger = new CapturingLogger();
        var client = CreateHttpClient(handler, logger);
        await client.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                50m,
                "Pedido",
                "11111111-1111-1111-1111-111111111111",
                "buyer@test.com",
                "Ana",
                null,
                null,
                "visa",
                "credit_card",
                "SECRET_CARD_TOKEN_XYZ",
                1,
                null,
                null),
            Guid.NewGuid().ToString("N")[..32]);

        using var doc = JsonDocument.Parse(capturedBody!);
        var pm = doc.RootElement.GetProperty("transactions").GetProperty("payments")[0]
            .GetProperty("payment_method");
        pm.GetProperty("type").GetString().Should().Be("credit_card");
        pm.GetProperty("token").GetString().Should().Be("SECRET_CARD_TOKEN_XYZ");
        pm.GetProperty("installments").GetInt32().Should().Be(1);
        capturedBody.Should().NotContain("411111");
        capturedBody.Should().NotContain("cvv");

        // Error path redact
        var errHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"code":"bad","message":"x","token":"SECRET_CARD_TOKEN_XYZ"}""",
                Encoding.UTF8,
                "application/json")
        });
        var errLogger = new CapturingLogger();
        var errClient = CreateHttpClient(errHandler, errLogger);
        try
        {
            await errClient.CreatePaymentAsync(
                new MercadoPagoCreatePaymentCommand(
                    50m, null, Guid.NewGuid().ToString("D"), "a@b.com", null, null, null,
                    "visa", "credit_card", "SECRET_CARD_TOKEN_XYZ", 1, null, null),
                Guid.NewGuid().ToString("N")[..32]);
        }
        catch
        {
            // expected
        }

        string.Join('\n', errLogger.Messages).Should().NotContain("SECRET_CARD_TOKEN_XYZ");
        string.Join('\n', errLogger.Messages).Should().Contain("[REDACTED]");
    }

    [Fact]
    public async Task HttpClient_Boleto_MapsBolbradesco_AndParsesDigitable()
    {
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "ORDBOL000000000000001",
                      "status": "action_required",
                      "status_detail": "pending_waiting_payment",
                      "external_reference": "22222222-2222-2222-2222-222222222222",
                      "total_amount": "50.00",
                      "currency_id": "BRL",
                      "transactions": {
                        "payments": [{
                          "id": "PAYBOL000000000000001",
                          "payment_method": {
                            "id": "boleto",
                            "type": "ticket",
                            "ticket_url": "https://example.test/boleto",
                            "digitable_line": "23793.LINE",
                            "barcode_content": "23791BAR"
                          }
                        }]
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = CreateHttpClient(handler, new CapturingLogger());
        var snap = await client.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                50m,
                "Pedido",
                "22222222-2222-2222-2222-222222222222",
                "buyer@test.com",
                "Joao",
                "Silva",
                "19119119100",
                "bolbradesco",
                "ticket",
                null,
                null,
                null,
                null,
                false,
                "01310100",
                "Av Paulista",
                "1000",
                "Bela Vista",
                "Sao Paulo",
                "SP",
                null),
            Guid.NewGuid().ToString("N")[..32]);

        using var doc = JsonDocument.Parse(capturedBody!);
        var pm = doc.RootElement.GetProperty("transactions").GetProperty("payments")[0]
            .GetProperty("payment_method");
        pm.GetProperty("id").GetString().Should().Be("boleto");
        pm.GetProperty("type").GetString().Should().Be("ticket");
        snap.DigitableLine.Should().Be("23793.LINE");
        snap.BarcodeContent.Should().Be("23791BAR");
        snap.TicketUrl.Should().Contain("boleto");
    }

    [Fact]
    public async Task HttpClient_Debit_OmitsInstallments()
    {
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "ORDDEB000000000000001",
                      "status": "processed",
                      "status_detail": "accredited",
                      "external_reference": "33333333-3333-3333-3333-333333333333",
                      "total_amount": "50.00",
                      "currency_id": "BRL",
                      "transactions": { "payments": [{ "id": "PAYDEB000000000000001" }] }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = CreateHttpClient(handler, new CapturingLogger());
        await client.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                50m, null, "33333333-3333-3333-3333-333333333333", "a@b.com",
                null, null, null, "elo", "debit_card", "tok_d", null, null, null),
            Guid.NewGuid().ToString("N")[..32]);

        using var doc = JsonDocument.Parse(capturedBody!);
        var pm = doc.RootElement.GetProperty("transactions").GetProperty("payments")[0]
            .GetProperty("payment_method");
        pm.GetProperty("type").GetString().Should().Be("debit_card");
        pm.TryGetProperty("installments", out _).Should().BeFalse();
        pm.GetProperty("token").GetString().Should().Be("tok_d");
    }

    private async Task<(OrderDto Order, string Auth)> CreateAwaitingOrderAsync(string paymentMethod)
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"mm{Guid.NewGuid():N}@test.com");
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
        string? idempotencyKey = null)
    {
        TestHelpers.SetBearerToken(_client, auth);
        using var payReq = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(body)
        };
        payReq.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            idempotencyKey ?? $"pay-{Guid.NewGuid():N}"[..32]);
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

    private static MercadoPagoHttpClient CreateHttpClient(
        HttpMessageHandler handler,
        CapturingLogger logger)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.com/") };
        var options = Options.Create(new Application.Options.MercadoPagoOptions
        {
            AccessToken = "TEST_ACCESS_TOKEN_VALUE",
            Environment = "Test",
            EnvironmentKind = Application.Options.MercadoPagoEnvironmentKind.Test,
            SandboxPixEnabled = true,
            SandboxPixAmount = 50.00m
        });
        return new MercadoPagoHttpClient(http, options, logger);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private sealed class CapturingLogger : ILogger<MercadoPagoHttpClient>
    {
        public List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
