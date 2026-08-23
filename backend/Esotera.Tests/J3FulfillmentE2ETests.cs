using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.J3;
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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// Dry-run E2E em SQLite relacional: webhook MP → Pending no mesmo request → processor manual.
/// EF InMemory não é autoritativo para webhook→Pending (1:1 + RowVersion).
/// Zero HTTP J3 real. Processor NÃO é disparado pelo webhook.
/// </summary>
[Collection("sqlite-j3-e2e")]
public class J3FulfillmentE2ETests
{
    private readonly SqliteJ3FulfillmentEnabledWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    public J3FulfillmentE2ETests(SqliteJ3FulfillmentEnabledWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void TestingHost_UsesFakeJ3Clients_NoWorker_NoRealHttpClient()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IJ3Client>().Should().BeOfType<FakeJ3Client>();
        sp.GetRequiredService<IJ3FulfillmentClient>().Should().BeOfType<FakeJ3FulfillmentClient>();
        sp.GetService<J3Client>().Should().BeNull();
        sp.GetService<J3FulfillmentHttpClient>().Should().BeNull();

        typeof(PaymentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IJ3FulfillmentProcessor));

        sp.GetServices<IHostedService>()
            .Select(s => s.GetType().Name)
            .Should().NotContain(n => n.Contains("J3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultFactory_FulfillmentFlag_RemainsFalse()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        opts.FulfillmentEnabled.Should().BeFalse();
        opts.CanFulfill.Should().BeFalse();
    }

    [Fact]
    public async Task PaidJ3_CreatesPending_AttemptCountZero_WebhookDoesNotCallProcessor()
    {
        var fake = ResetFakes();
        var orderId = await CreateAndPayJ3OrderAsync();

        fake.Mut.CreateCallCount.Should().Be(0, "webhook só chama EnsurePending, nunca o processor");

        var admin = await AdminGetByOrderAsync(orderId);
        admin.Should().NotBeNull();
        admin!.Status.Should().Be(J3FulfillmentStatus.Pending);
        admin.AttemptCount.Should().Be(0);
        admin.J3OrderId.Should().BeNull();
        admin.CanRetrySafely.Should().BeFalse();
        admin.NeedsManualReview.Should().BeFalse();
    }

    [Fact]
    public async Task PaidJ3_FakeSuccess_AdminSeesCreatedIds()
    {
        var fake = ResetFakes();
        fake.Mut.NextResult = J3CreateOrderAttemptResult.Success(
            "e2e-oid", "e2e-code", "e2e-trk", "e2e-dp");

        var orderId = await CreateAndPayJ3OrderAsync();
        await SeedAuthorizedFiscalAsync(orderId);
        var pending = await AdminGetByOrderAsync(orderId);
        pending!.Status.Should().Be(J3FulfillmentStatus.Pending);
        pending.AttemptCount.Should().Be(0);

        await ProcessAsync(pending.Id);

        fake.Mut.CreateCallCount.Should().Be(1);
        fake.Mut.Should().BeOfType<FakeJ3FulfillmentClient>();

        var admin = await AdminGetByOrderAsync(orderId);
        admin!.Status.Should().Be(J3FulfillmentStatus.Created);
        admin.AttemptCount.Should().Be(1);
        admin.J3OrderId.Should().Be("e2e-oid");
        admin.J3OrderCode.Should().Be("e2e-code");
        admin.J3TrackingNumber.Should().Be("e2e-trk");
        admin.J3DeliveryPointId.Should().Be("e2e-dp");
        admin.CanRetrySafely.Should().BeFalse();
        admin.NeedsManualReview.Should().BeFalse();
        admin.IsPossiblyStuck.Should().BeFalse();
    }

    [Fact]
    public async Task PaidJ3_FakeUnknown_IsTerminal_AdminNeedsReview_NoSecondCall()
    {
        var fake = ResetFakes();
        fake.Mut.NextResult = J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.TimeoutUnknown);

        var orderId = await CreateAndPayJ3OrderAsync();
        await SeedAuthorizedFiscalAsync(orderId);
        var pending = await AdminGetByOrderAsync(orderId);
        await ProcessAsync(pending!.Id);

        fake.Mut.CreateCallCount.Should().Be(1);
        var after = await AdminGetByOrderAsync(orderId);
        after!.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        after.AttemptCount.Should().Be(1);
        after.CanRetrySafely.Should().BeFalse();
        after.NeedsManualReview.Should().BeTrue();

        await ProcessAsync(pending.Id);
        fake.Mut.CreateCallCount.Should().Be(1);
        var again = await AdminGetByOrderAsync(orderId);
        again!.Status.Should().Be(J3FulfillmentStatus.UnknownOutcome);
        again.AttemptCount.Should().Be(1);
        again.CanRetrySafely.Should().BeFalse();
        again.NeedsManualReview.Should().BeTrue();
    }

    [Fact]
    public async Task PaidJ3_ResidentialNull_SkippedBeforeClaim_ZeroClient_StaysPending()
    {
        var fake = ResetFakes();
        var orderId = await CreateAndPayJ3OrderAsync();
        await SeedAuthorizedFiscalAsync(orderId);
        var pending = await AdminGetByOrderAsync(orderId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.ShippingIsResidentialAddress = null;
            await db.SaveChangesAsync();
        }

        await ProcessAsync(pending!.Id);

        fake.Mut.CreateCallCount.Should().Be(0);
        var admin = await AdminGetByOrderAsync(orderId);
        admin!.Status.Should().Be(J3FulfillmentStatus.Pending);
        admin.AttemptCount.Should().Be(0);
        admin.CanRetrySafely.Should().BeFalse();
        admin.CanSendToJ3.Should().BeFalse();
        admin.EligibilityReason.Should().Be(J3FulfillmentEligibilityCodes.MissingResidentialFlag);
    }

    [Fact]
    public async Task UnpaidJ3_DoesNotCreateFulfillment()
    {
        // Comportamento atual: EnsurePending só roda se Status == payment_approved
        // (hook no PaymentService após webhook/PIX processado). Pedido awaiting_payment
        // não cria J3Fulfillment.
        var fake = ResetFakes();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"e2eunp{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var create = await TestHelpers.PostOrderAsync(_client, J3OrderRequest());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Status.Should().Be(OrderStatus.AwaitingPayment);

        var admin = await AdminGetByOrderAsync(order.Id);
        admin.Should().BeNull();
        fake.Mut.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PaidNonJ3_NoFulfillment_NoFulfillmentClient()
    {
        var fake = ResetFakes();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"e2epac{Guid.NewGuid():N}@test.com");
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
        await ApproveViaWebhookAsync(order!.Id, token);

        var get = await _client.GetAsync($"/api/orders/{order.Id}");
        var paid = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        paid!.Status.Should().Be(OrderStatus.PaymentApproved);

        (await AdminGetByOrderAsync(order.Id)).Should().BeNull();
        fake.Mut.CreateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentProcess_Sqlite_OneFakeMutation_AdminSeesCreated()
    {
        await using var harness = await SqliteE2EHarness.CreateAsync();
        var fulfillmentId = await harness.SeedPendingJ3Async();
        harness.Fake.Reset();
        harness.Fake.NextResult = J3CreateOrderAttemptResult.Success(
            "rel-oid", "rel-code", "rel-trk", "rel-dp");

        var t1 = Task.Run(async () =>
        {
            await using var ctx = harness.CreateContext();
            await harness.CreateProcessor(ctx).ProcessAsync(fulfillmentId);
        });
        var t2 = Task.Run(async () =>
        {
            await using var ctx = harness.CreateContext();
            await harness.CreateProcessor(ctx).ProcessAsync(fulfillmentId);
        });
        await Task.WhenAll(t1, t2);

        harness.Fake.CreateCallCount.Should().Be(1);
        harness.Fake.Should().BeOfType<FakeJ3FulfillmentClient>();

        await using var verify = harness.CreateContext();
        var admin = harness.CreateAdminQuery(verify);
        var dto = await admin.GetAsync(fulfillmentId);
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(J3FulfillmentStatus.Created);
        dto.AttemptCount.Should().Be(1);
        dto.J3OrderId.Should().Be("rel-oid");
        dto.J3TrackingNumber.Should().Be("rel-trk");
        dto.CanRetrySafely.Should().BeFalse();
        dto.NeedsManualReview.Should().BeFalse();
    }

    private (FakeJ3FulfillmentClient Mut, FakeJ3Client Read) ResetFakes()
    {
        using var scope = _factory.Services.CreateScope();
        var mut = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var read = scope.ServiceProvider.GetRequiredService<FakeJ3Client>();
        mut.Reset();
        mut.Should().BeOfType<FakeJ3FulfillmentClient>();
        read.Should().BeOfType<FakeJ3Client>();
        return (mut, read);
    }

    private async Task ProcessAsync(Guid fulfillmentId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IJ3FulfillmentProcessor>()
            .ProcessAsync(fulfillmentId);
    }

    private async Task<J3FulfillmentAdminDetailDto?> AdminGetByOrderAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IJ3FulfillmentAdminQueryService>();
        var page = await queries.ListAsync(new J3FulfillmentFilterRequest(null, orderId, null, 1, 20));
        if (page.Items.Count == 0)
            return null;
        return await queries.GetAsync(page.Items[0].Id);
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

    private async Task<Guid> CreateAndPayJ3OrderAsync()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"e2ej3{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var create = await TestHelpers.PostOrderAsync(_client, J3OrderRequest());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await create.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        await ApproveViaWebhookAsync(order!.Id, token);
        return order.Id;
    }

    private async Task SeedAuthorizedFiscalAsync(Guid orderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var hex = Guid.NewGuid().ToString("N");
        Span<char> digits = stackalloc char[44];
        "35260820".AsSpan().CopyTo(digits);
        for (var i = 8; i < 44; i++)
            digits[i] = (char)('0' + (hex[(i - 8) % hex.Length] % 10));
        var now = DateTime.UtcNow;
        db.FiscalInvoices.Add(new FiscalInvoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = FiscalInvoiceStatus.Authorized,
            ChNFe = new string(digits),
            Number = "2",
            Series = "9",
            AuthorizedAtUtc = now,
            XmlCipher = "test-cipher",
            XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
            Source = FiscalInvoiceSource.ManualUpload,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }

    private async Task ApproveViaWebhookAsync(Guid orderId, string customerToken)
    {
        await TestHelpers.ForceOrderTotalAsync(_factory.Services, orderId, 50.00m);
        TestHelpers.SetBearerToken(_client, customerToken);

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

        using var hook = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/mercadopago?data.id={dataId}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        hook.Headers.TryAddWithoutValidation("x-signature", $"ts={ts},v1={v1}");
        hook.Headers.TryAddWithoutValidation("x-request-id", requestId);
        var hookRes = await _client.SendAsync(hook);
        hookRes.StatusCode.Should().Be(HttpStatusCode.OK);

        TestHelpers.SetBearerToken(_client, customerToken);
        var get = await _client.GetAsync($"/api/orders/{orderId}");
        var after = await get.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        after!.Status.Should().Be(OrderStatus.PaymentApproved);
        // Pending nasce no PaymentService após SaveChanges do webhook — sem EnsurePending extra.
    }

    private sealed class SqliteE2EHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EsoteraDbContext> _options;
        private readonly IOptions<J3ShippingOptions> _j3Options;

        public FakeJ3FulfillmentClient Fake { get; } = new();
        public FakeJ3OrderDetailsClient DetailsFake { get; } = new();

        private SqliteE2EHarness(
            SqliteConnection connection,
            DbContextOptions<EsoteraDbContext> options,
            IOptions<J3ShippingOptions> j3Options)
        {
            _connection = connection;
            _options = options;
            _j3Options = j3Options;
        }

        public static async Task<SqliteE2EHarness> CreateAsync()
        {
            var connection = new SqliteConnection($"DataSource=file:j3e2e_{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EsoteraDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var db = new EsoteraDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var j3 = Options.Create(new J3ShippingOptions
            {
                FulfillmentEnabled = true,
                SellerId = "test-seller-id",
                SellerInformationId = "test-seller-info"
            });
            return new SqliteE2EHarness(connection, options, j3);
        }

        public EsoteraDbContext CreateContext() => new(_options);

        public J3FulfillmentProcessor CreateProcessor(EsoteraDbContext db)
        {
            var fulfillment = new J3FulfillmentService(
                db, _j3Options, NullLogger<J3FulfillmentService>.Instance);
            var eligibility = new J3FulfillmentEligibilityService(db, _j3Options);
            var hydration = new J3IdentifierHydrationService(
                db,
                DetailsFake,
                NullLogger<J3IdentifierHydrationService>.Instance);
            return new J3FulfillmentProcessor(
                db,
                fulfillment,
                Fake,
                eligibility,
                hydration,
                _j3Options,
                NullLogger<J3FulfillmentProcessor>.Instance);
        }

        public J3FulfillmentAdminQueryService CreateAdminQuery(EsoteraDbContext db) =>
            new(db, new J3FulfillmentEligibilityService(db, _j3Options), _j3Options);

        public async Task<Guid> SeedPendingJ3Async()
        {
            await using var db = CreateContext();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                Name = "E2E Rel",
                Email = $"e2erel-{userId:N}@example.com",
                PasswordHash = "x",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            var orderId = Guid.NewGuid();
            db.Orders.Add(new Order
            {
                Id = orderId,
                OrderNumber = $"E2E-{orderId.ToString("N")[..8]}",
                UserId = userId,
                Status = OrderStatus.PaymentApproved,
                Subtotal = 50,
                Discount = 0,
                ShippingPrice = 12.99m,
                Total = 62.99m,
                ShippingMethodId = ShippingMethod.J3,
                ShippingMethodName = "J3",
                ShippingProvider = "J3",
                ShipCep = "01310100",
                ShipStreet = "Av Paulista",
                ShipNumber = "1000",
                ShipNeighborhood = "Bela Vista",
                ShipCity = "São Paulo",
                ShipState = "SP",
                ShippingIsResidentialAddress = true,
                PaymentMethod = "pix",
                PaymentStatus = "approved",
                CustomerName = "Cliente",
                CustomerEmail = "c@example.com",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            var hex = Guid.NewGuid().ToString("N");
            Span<char> digits = stackalloc char[44];
            "35260820".AsSpan().CopyTo(digits);
            for (var i = 8; i < 44; i++)
                digits[i] = (char)('0' + (hex[(i - 8) % hex.Length] % 10));
            var now = DateTime.UtcNow;
            db.FiscalInvoices.Add(new FiscalInvoice
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = FiscalInvoiceStatus.Authorized,
                ChNFe = new string(digits),
                Number = "2",
                Series = "9",
                AuthorizedAtUtc = now,
                XmlCipher = "test-cipher",
                XmlSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..32],
                Source = FiscalInvoiceSource.ManualUpload,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            var fulfillmentId = Guid.NewGuid();
            db.J3Fulfillments.Add(new J3Fulfillment
            {
                Id = fulfillmentId,
                OrderId = orderId,
                Status = J3FulfillmentStatus.Pending,
                AttemptCount = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
            return fulfillmentId;
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
