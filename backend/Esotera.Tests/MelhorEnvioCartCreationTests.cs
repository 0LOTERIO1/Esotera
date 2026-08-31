using Esotera.Application.DTOs.Integrations;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esotera.Tests;

/// <summary>
/// Fase C1: inserção do frete no CARRINHO do Melhor Envio.
///
/// Garantia estrutural: IMelhorEnvioShipmentClient não tem método de checkout,
/// generate ou print — não existe caminho de código capaz de comprar etiqueta.
/// </summary>
public class MelhorEnvioCartCreationTests
{
    [Fact]
    public async Task ReadyToCreate_Success_MovesToCartCreated_AndSavesIds()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeTrue();
        result.ShipmentId.Should().Be(FakeMelhorEnvioShipmentClient.FakeCartShipmentId);
        result.Protocol.Should().Be(FakeMelhorEnvioShipmentClient.FakeCartProtocol);
        harness.Client.CartCallCount.Should().Be(1);

        await using var db = harness.CreateContext();
        var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.CartCreated);
        row.MelhorEnvioShipmentId.Should().Be(FakeMelhorEnvioShipmentClient.FakeCartShipmentId);
        row.MelhorEnvioProtocol.Should().Be(FakeMelhorEnvioShipmentClient.FakeCartProtocol);
        row.CartCreatedAtUtc.Should().NotBeNull();
        row.LastSyncErrorCode.Should().BeNull();
        row.LastSyncErrorMessage.Should().BeNull();

        // Fase C1 não compra nem gera etiqueta.
        row.PurchasedAtUtc.Should().BeNull();
        row.LabelGeneratedAtUtc.Should().BeNull();
        row.LabelUrl.Should().BeNull();
    }

    [Fact]
    public async Task Success_ClearsPreviousError()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();

        await using (var db = harness.CreateContext())
        {
            var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
            row.LastSyncErrorCode = "OLD_ERROR";
            row.LastSyncErrorMessage = "falha anterior";
            await db.SaveChangesAsync();
        }

        (await harness.ProcessAsync(orderId)).Ok.Should().BeTrue();

        await using var verify = harness.CreateContext();
        var updated = await verify.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        updated.LastSyncErrorCode.Should().BeNull();
        updated.LastSyncErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task WaitingInvoice_IsNotProcessed_AndNoHttpCall()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync(
            shipmentStatus: MelhorEnvioShipmentStatus.WaitingInvoice);

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeFalse();
        result.BlockedLocally.Should().BeTrue();
        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.StatusNotReady);
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WithoutAuthorizedInvoice_IsBlocked_AndNoHttpCall()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync(withAuthorizedInvoice: false);

        var result = await harness.ProcessAsync(orderId);

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.InvoiceNotAuthorized);
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PaymentNotApproved_IsBlocked_AndNoHttpCall()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync(orderStatus: OrderStatus.AwaitingPayment);

        var result = await harness.ProcessAsync(orderId);

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.PaymentNotApproved);
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AlreadyHasShipmentId_DoesNotCreateAnother()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();

        await using (var db = harness.CreateContext())
        {
            var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
            row.MelhorEnvioShipmentId = "already-there";
            row.Status = MelhorEnvioShipmentStatus.CartCreated;
            await db.SaveChangesAsync();
        }

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeTrue();
        result.AlreadyCreated.Should().BeTrue();
        result.ShipmentId.Should().Be("already-there");
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessTwice_CallsMelhorEnvioOnlyOnce()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();

        (await harness.ProcessAsync(orderId)).Ok.Should().BeTrue();
        var second = await harness.ProcessAsync(orderId);

        second.AlreadyCreated.Should().BeTrue();
        harness.Client.CartCallCount.Should().Be(1);
    }

    [Fact]
    public async Task MissingCartWriteScope_ReturnsReauthorizeMessage_AndNoHttpCall()
    {
        await using var harness = await CartHarness.CreateAsync(
            connectionScopes: MelhorEnvioOptions.RequiredScope);
        var orderId = await harness.SeedReadyOrderAsync();

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.ScopeMissing);
        result.ErrorMessage.Should()
            .Be("Reautorize o Melhor Envio com os novos escopos antes de criar envio.");
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnvironmentMismatch_IsBlocked_AndNoHttpCall()
    {
        await using var harness = await CartHarness.CreateAsync(connectionEnvironment: "sandbox");
        var orderId = await harness.SeedReadyOrderAsync();

        var result = await harness.ProcessAsync(orderId);

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.EnvironmentMismatch);
        harness.Client.CartCallCount.Should().Be(0);
    }

    [Fact]
    public async Task IncompleteSender_IsBlocked_AndRecordsError_KeepingReadyToCreate()
    {
        await using var harness = await CartHarness.CreateAsync(completeSender: false);
        var orderId = await harness.SeedReadyOrderAsync();

        var result = await harness.ProcessAsync(orderId);

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.SenderIncomplete);
        harness.Client.CartCallCount.Should().Be(0);

        await using var db = harness.CreateContext();
        var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        // Nada foi enviado: continua acionável depois de configurar o remetente.
        row.Status.Should().Be(MelhorEnvioShipmentStatus.ReadyToCreate);
        row.LastSyncErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.SenderIncomplete);
        row.LastSyncErrorMessage.Should().Contain("inscrição estadual");
    }

    [Fact]
    public async Task Forbidden_RevertsToReadyToCreate_WithReauthorizeMessage()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();
        harness.Client.CartOutcome = _ => new MelhorEnvioCartOutcome
        {
            Forbidden = true,
            ErrorCode = MelhorEnvioShipmentErrorCodes.Forbidden,
            ErrorMessage = "forbidden"
        };

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeFalse();
        result.Status.Should().Be(MelhorEnvioShipmentStatus.ReadyToCreate);

        await using var db = harness.CreateContext();
        var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.ReadyToCreate);
        row.LastSyncErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.Forbidden);
        row.LastSyncErrorMessage.Should().Contain("Reautorize");
        row.MelhorEnvioShipmentId.Should().BeNull();
    }

    [Fact]
    public async Task ValidationRejected_MovesToFailed_AndRecordsError()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();
        harness.Client.CartOutcome = _ => new MelhorEnvioCartOutcome
        {
            ValidationRejected = true,
            ErrorCode = MelhorEnvioShipmentErrorCodes.ValidationRejected,
            ErrorMessage = "to.postal_code: campo inválido"
        };

        var result = await harness.ProcessAsync(orderId);

        result.Ok.Should().BeFalse();

        await using var db = harness.CreateContext();
        var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.Failed);
        row.LastSyncErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.ValidationRejected);
        row.LastSyncErrorMessage.Should().Contain("postal_code");
        row.LastSyncAtUtc.Should().NotBeNull();
        row.MelhorEnvioShipmentId.Should().BeNull();
    }

    [Fact]
    public async Task Timeout_MovesToFailed_BecauseOutcomeIsUnknown()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();
        harness.Client.CartOutcome = _ => new MelhorEnvioCartOutcome
        {
            TimedOut = true,
            ErrorCode = MelhorEnvioShipmentErrorCodes.Timeout,
            ErrorMessage = "timeout"
        };

        await harness.ProcessAsync(orderId);

        await using var db = harness.CreateContext();
        var row = await db.MelhorEnvioShipments.SingleAsync(s => s.OrderId == orderId);
        row.Status.Should().Be(MelhorEnvioShipmentStatus.Failed);
        row.LastSyncErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.Timeout);
    }

    [Fact]
    public async Task SuccessfulPayload_CarriesServiceInvoiceKeyAndProducts()
    {
        await using var harness = await CartHarness.CreateAsync();
        var orderId = await harness.SeedReadyOrderAsync();

        await harness.ProcessAsync(orderId);

        var payload = harness.Client.LastCartRequest;
        payload.Should().NotBeNull();
        payload!.Service.Should().Be(2);
        payload.Options.InvoiceKey.Should().Be(new string('1', 44));
        payload.Options.NonCommercial.Should().BeFalse();
        payload.Products.Should().HaveCount(1);
        payload.Products[0].Quantity.Should().Be(2);
        payload.Products[0].UnitaryValue.Should().Be(50m);
        // Valor segurado = mercadorias, sem frete.
        payload.Options.InsuranceValue.Should().Be(100m);
        payload.Volumes.Should().HaveCount(1);
        payload.Volumes[0].WeightKg.Should().Be(0.4m);
        payload.From.CompanyDocument.Should().Be("46867029000176");
        payload.From.StateRegister.Should().Be("123456789");
        payload.From.PostalCode.Should().Be("08061420");
        payload.To.PostalCode.Should().Be("03065000");
        payload.To.StateRegister.Should().Be("ISENTO");
        payload.To.CountryId.Should().Be("BR");
    }

    // --- Builder puro ---

    [Fact]
    public void Builder_WithoutInvoiceKey_Fails()
    {
        var result = MelhorEnvioCartPayloadBuilder.Build(
            CartHarness.BuildOrderGraph(),
            invoiceKey: null,
            CartHarness.BuildSettings(),
            CartHarness.BuildSender(complete: true));

        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.InvoiceKeyMissing);
    }

    [Fact]
    public void Builder_WithShortInvoiceKey_Fails()
    {
        var result = MelhorEnvioCartPayloadBuilder.Build(
            CartHarness.BuildOrderGraph(),
            invoiceKey: "123",
            CartHarness.BuildSettings(),
            CartHarness.BuildSender(complete: true));

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.InvoiceKeyMissing);
    }

    [Fact]
    public void Builder_WithoutServiceId_Fails()
    {
        var order = CartHarness.BuildOrderGraph();
        order.ShippingServiceId = null;

        var result = MelhorEnvioCartPayloadBuilder.Build(
            order,
            new string('1', 44),
            CartHarness.BuildSettings(),
            CartHarness.BuildSender(complete: true));

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.ServiceIdMissing);
    }

    [Fact]
    public void Builder_WithoutItems_Fails()
    {
        var order = CartHarness.BuildOrderGraph();
        order.Items.Clear();

        var result = MelhorEnvioCartPayloadBuilder.Build(
            order,
            new string('1', 44),
            CartHarness.BuildSettings(),
            CartHarness.BuildSender(complete: true));

        result.ErrorCode.Should().Be(MelhorEnvioShipmentErrorCodes.ItemsMissing);
    }

    [Fact]
    public void Builder_MasksAndStripsNonDigits_FromDocumentsAndCeps()
    {
        var order = CartHarness.BuildOrderGraph();
        order.ShipCep = "03065-000";
        order.CustomerCpf = "123.456.789-09";

        var result = MelhorEnvioCartPayloadBuilder.Build(
            order,
            new string('1', 44),
            CartHarness.BuildSettings(),
            CartHarness.BuildSender(complete: true));

        result.Ok.Should().BeTrue();
        result.Request!.To.PostalCode.Should().Be("03065000");
        result.Request.To.Document.Should().Be("12345678909");
    }

    [Fact]
    public void ErrorCodes_SanitizeStripsUnsafeCharacters()
    {
        MelhorEnvioShipmentErrorCodes.Sanitize("validation rejected!").Should().Be("VALIDATION_REJECTED");
        MelhorEnvioShipmentErrorCodes.Sanitize("   ").Should().Be(MelhorEnvioShipmentErrorCodes.Unexpected);
        MelhorEnvioShipmentErrorCodes.Sanitize(new string('A', 200)).Length.Should().Be(64);
    }

    [Fact]
    public void RequestedScopes_DoNotIncludeCheckout()
    {
        // Guarda-vida: shipping-checkout permitiria debitar a carteira.
        MelhorEnvioOptions.RequestedScopes.Should().Be("shipping-calculate cart-write");
        MelhorEnvioOptions.RequestedScopes.Should().NotContain("checkout");
    }

    [Fact]
    public void HasAllScopes_RejectsLegacyConnection()
    {
        MelhorEnvioOptions
            .HasAllScopes("shipping-calculate", MelhorEnvioOptions.CartCreationScopes)
            .Should().BeFalse();
        MelhorEnvioOptions
            .HasAllScopes("shipping-calculate cart-write", MelhorEnvioOptions.CartCreationScopes)
            .Should().BeTrue();
        MelhorEnvioOptions
            .HasAllScopes(null, MelhorEnvioOptions.CartCreationScopes)
            .Should().BeFalse();
    }

    private sealed class CartHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EsoteraDbContext> _options;
        private readonly MelhorEnvioSenderOptions _sender;
        private readonly string _connectionScopes;
        private readonly string _connectionEnvironment;

        public FakeMelhorEnvioShipmentClient Client { get; } = new();

        private CartHarness(
            SqliteConnection connection,
            DbContextOptions<EsoteraDbContext> options,
            MelhorEnvioSenderOptions sender,
            string connectionScopes,
            string connectionEnvironment)
        {
            _connection = connection;
            _options = options;
            _sender = sender;
            _connectionScopes = connectionScopes;
            _connectionEnvironment = connectionEnvironment;
        }

        public static async Task<CartHarness> CreateAsync(
            bool completeSender = true,
            string? connectionScopes = null,
            string connectionEnvironment = "production")
        {
            var connection = new SqliteConnection(
                $"DataSource=file:mecart_{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<EsoteraDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var db = new EsoteraDbContext(options))
                await db.Database.EnsureCreatedAsync();

            var harness = new CartHarness(
                connection,
                options,
                BuildSender(completeSender),
                connectionScopes ?? MelhorEnvioOptions.RequestedScopes,
                connectionEnvironment);

            await harness.SeedInfrastructureAsync();
            return harness;
        }

        public EsoteraDbContext CreateContext() => new(_options);

        public async Task<MelhorEnvioCartCreationResult> ProcessAsync(Guid orderId)
        {
            await using var db = CreateContext();
            var service = new MelhorEnvioShipmentProcessingService(
                db,
                new StubOAuthService(),
                Client,
                Options.Create(FullyConfiguredOptions()),
                Options.Create(_sender),
                NullLogger<MelhorEnvioShipmentProcessingService>.Instance);

            return await service.CreateCartShipmentAsync(orderId);
        }

        private static MelhorEnvioOptions FullyConfiguredOptions() => new()
        {
            Enabled = true,
            Environment = "production",
            ClientId = "id",
            ClientSecret = "secret",
            RedirectUri = "https://api.example.com/callback",
            UserAgent = "Esotera (test@example.com)",
            FrontendBaseUrl = "https://example.com"
        };

        public static MelhorEnvioSenderOptions BuildSender(bool complete) =>
            complete
                ? new MelhorEnvioSenderOptions
                {
                    Name = "Esotera Livraria",
                    Email = "loja@example.com",
                    Phone = "11912345678",
                    CompanyDocument = "46.867.029/0001-76",
                    StateRegister = "123456789",
                    EconomicActivityCode = "4761001",
                    Address = "Rua da Loja",
                    Number = "100",
                    District = "Centro",
                    City = "São Paulo",
                    StateAbbr = "sp",
                    Platform = "Esotera"
                }
                : new MelhorEnvioSenderOptions { Name = "Esotera Livraria" };

        public static StoreSettings BuildSettings() => new()
        {
            Id = 1,
            ShippingOriginCep = "08061420",
            PackageHeightCm = 6m,
            PackageWidthCm = 11m,
            PackageLengthCm = 16m,
            PackageWeightGrams = 400,
            UpdatedAtUtc = DateTime.UtcNow
        };

        /// <summary>Grafo de pedido em memória para testar o builder isoladamente.</summary>
        public static Order BuildOrderGraph()
        {
            var orderId = Guid.NewGuid();
            return new Order
            {
                Id = orderId,
                OrderNumber = "ME-0001",
                Status = OrderStatus.PaymentApproved,
                ShippingMethodId = ShippingMethod.MelhorExpresso,
                ShippingMethodName = "Melhor Envio - Expresso",
                ShippingProvider = "melhor_envio",
                ShippingPrice = 24.90m,
                ShippingServiceId = 2,
                ShippingServiceName = "SEDEX",
                ShippingCarrierName = "Correios",
                ShipCep = "03065000",
                ShipStreet = "Rua do Cliente",
                ShipNumber = "42",
                ShipNeighborhood = "Bairro",
                ShipCity = "São Paulo",
                ShipState = "SP",
                CustomerName = "Cliente Teste",
                CustomerEmail = "cliente@example.com",
                CustomerPhone = "11987654321",
                CustomerCpf = "12345678909",
                PaymentMethod = "pix",
                PaymentStatus = "approved",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Items =
                [
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        ProductId = Guid.NewGuid(),
                        ProductName = "Tarô de Waite",
                        Quantity = 2,
                        UnitPrice = 50m
                    }
                ]
            };
        }

        private async Task SeedInfrastructureAsync()
        {
            await using var db = CreateContext();
            db.StoreSettings.Add(BuildSettings());
            var now = DateTime.UtcNow;
            db.MelhorEnvioConnections.Add(new MelhorEnvioConnection
            {
                Id = Guid.NewGuid(),
                AccessTokenCipher = "cipher-access",
                RefreshTokenCipher = "cipher-refresh",
                AccessTokenExpiresAtUtc = now.AddDays(20),
                RefreshTokenExpiresAtUtc = now.AddDays(40),
                ConnectedAtUtc = now,
                UpdatedAtUtc = now,
                Scopes = _connectionScopes,
                Environment = _connectionEnvironment
            });
            await db.SaveChangesAsync();
        }

        public async Task<Guid> SeedReadyOrderAsync(
            string orderStatus = OrderStatus.PaymentApproved,
            string shipmentStatus = MelhorEnvioShipmentStatus.ReadyToCreate,
            bool withAuthorizedInvoice = true)
        {
            await using var db = CreateContext();
            var userId = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = userId,
                Name = "Cart Test",
                Email = $"cart-{userId:N}@example.com",
                PasswordHash = "x",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            var order = BuildOrderGraph();
            order.UserId = userId;
            order.Status = orderStatus;
            order.OrderNumber = $"ME-{order.Id.ToString("N")[..8]}";
            // Sem Product real: o item já carrega o snapshot (nome/preço) que o payload usa.
            order.Items.First().ProductId = null;
            db.Orders.Add(order);

            var now = DateTime.UtcNow;
            db.MelhorEnvioShipments.Add(new MelhorEnvioShipment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Environment = "production",
                Status = shipmentStatus,
                ServiceId = 2,
                ServiceName = "SEDEX",
                CarrierName = "Correios",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            if (withAuthorizedInvoice)
            {
                db.FiscalInvoices.Add(new FiscalInvoice
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = FiscalInvoiceStatus.Authorized,
                    ChNFe = new string('1', 44),
                    XmlCipher = "cipher",
                    XmlSha256 = Guid.NewGuid().ToString("N"),
                    Source = FiscalInvoiceSource.ManualUpload,
                    AuthorizedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            await db.SaveChangesAsync();
            return order.Id;
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    /// <summary>Entrega um token fixo. Nunca faz HTTP nem toca no banco.</summary>
    private sealed class StubOAuthService : IMelhorEnvioOAuthService
    {
        public Task<MelhorEnvioAuthorizeResponse> CreateAuthorizationUrlAsync(
            Guid adminUserId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string> HandleCallbackAsync(
            string? code,
            string? state,
            string? error,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MelhorEnvioStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("stub-token");

        public Task<T> ExecuteWithTokenRetryAsync<T>(
            Func<string, CancellationToken, Task<T>> action,
            Func<T, bool> isUnauthenticated,
            CancellationToken cancellationToken = default) =>
            action("stub-token", cancellationToken);
    }
}
