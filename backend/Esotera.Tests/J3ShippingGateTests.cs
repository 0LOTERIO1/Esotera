using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Shipping;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// Passo 3 — J3 real via FakeJ3Client (zero rede / j3tms.com.br bloqueado por Fake).
/// </summary>
public class J3ShippingGateTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    // ── 1. Enabled=false ─────────────────────────────────────────

    [Fact]
    public async Task Quote_J3Disabled_DoesNotCallClient_OmitsJ3_MeStillAvailable()
    {
        await using var factory = new J3DisabledWebApplicationFactory();
        var client = factory.CreateClient();
        var meFake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        meFake.Reset();
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Ok.Should().BeTrue();
        body.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id == "melhor_economico");
        body.Options.Should().Contain(o => o.Id == "melhor_expresso");
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    // ── 2. Enabled + preço 0 ─────────────────────────────────────

    [Fact]
    public async Task Quote_J3Enabled_ZeroPrice_DoesNotCallClient_OmitsJ3()
    {
        await using var factory = new J3EnabledZeroPriceWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Quote_J3Enabled_MissingPrice_DoesNotCallClient_OmitsJ3()
    {
        await using var factory = new J3EnabledMissingPriceWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    // ── 3. URL/token ausentes ────────────────────────────────────

    [Fact]
    public async Task Quote_J3Enabled_MissingUrl_OmitsJ3_MePreserved()
    {
        await using var factory = new J3EnabledMissingUrlWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id == "melhor_economico");
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Quote_J3Enabled_MissingToken_OmitsJ3_MePreserved()
    {
        await using var factory = new J3EnabledMissingTokenWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id == "melhor_economico");
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    // ── 4–5. coverage true/false ─────────────────────────────────

    [Fact]
    public async Task Quote_CoverageTrue_ReturnsJ3()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var meFake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        meFake.Reset();
        meFake.NetworkError = true;
        j3Fake.Reset();
        j3Fake.CoverageResult = true;

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().ContainSingle(o => o.Id == "j3");
        j3Fake.CoverageCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Quote_CoverageFalse_OmitsJ3()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageResult = false;

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        j3Fake.CoverageCallCount.Should().Be(1);
    }

    // ── 6–7. exception / timeout ─────────────────────────────────

    [Fact]
    public async Task Quote_CoverageException_OmitsJ3_DoesNotBreakQuote()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageException = new J3ApiException("IsValidServiceArea", "J3 IsValidServiceArea: HTTP 500.", httpStatus: 500);

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id == "melhor_economico");
    }

    [Fact]
    public async Task Quote_CoverageTimeout_OmitsJ3_Safely()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageException = new J3ApiException("IsValidServiceArea", "J3 IsValidServiceArea: request timed out.");

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id.StartsWith("melhor_"));
    }

    // ── 8. StandardPriceCents vs StoreSettings.J3Price ───────────

    [Fact]
    public async Task Quote_J3UsesStandardPriceCents_NotStoreSettingsJ3Price()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var meFake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        meFake.Reset();
        meFake.NetworkError = true;
        j3Fake.Reset();
        j3Fake.CoverageResult = true;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var settings = await db.StoreSettings.FirstAsync(s => s.Id == 1);
            settings.J3Price = 17.50m; // diferente de 12.99 (1299 cents)
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        var j3 = body!.Options.Should().ContainSingle(o => o.Id == "j3").Subject;
        j3.Price.Should().Be(12.99m);
        j3.Price.Should().NotBe(17.50m);
    }

    // ── 9–10. prazo null / isSameDay ─────────────────────────────

    [Fact]
    public async Task Quote_J3_UnknownDeadline_PrazoAConfirmar_NotSameDay()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var meFake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        meFake.Reset();
        meFake.NetworkError = true;
        j3Fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        var j3 = body!.Options.Should().ContainSingle(o => o.Id == "j3").Subject;
        j3.EstimatedDays.Should().Be("Prazo a confirmar");
        j3.EstimatedDaysMin.Should().BeNull();
        j3.EstimatedDaysMax.Should().BeNull();
        // Contrato FE: isSameDay = min != null && min === 0
        (j3.EstimatedDaysMin is 0).Should().BeFalse();
    }

    // ── 11. frete grátis ─────────────────────────────────────────

    [Fact]
    public async Task Quote_J3_FreeShipping_AppliesCommerceRules()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var meFake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        meFake.Reset();
        meFake.NetworkError = true;
        j3Fake.Reset();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var settings = await db.StoreSettings.FirstAsync(s => s.Id == 1);
            settings.FreeShippingMin = 40m;
            settings.FreeShippingStatesCsv = "SP";
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        var j3 = body!.Options.Should().ContainSingle(o => o.Id == "j3").Subject;
        j3.OriginalPrice.Should().Be(12.99m);
        j3.Price.Should().Be(0m);
        j3.FreeShippingApplied.Should().BeTrue();
    }

    // ── 12–15. CreateOrder ───────────────────────────────────────

    [Fact]
    public async Task CreateOrder_J3_RevalidatesCoverage_ServerSide()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageResult = true;

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"j3ok{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await TestHelpers.PostOrderAsync(client, BaseJ3Request());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        j3Fake.CoverageCallCount.Should().BeGreaterThan(0);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Shipping.MethodId.Should().Be("j3");
        order.Shipping.EstimatedDays.Should().BeNull();
        order.ShippingPrice.Should().Be(12.99m);
    }

    [Fact]
    public async Task CreateOrder_J3_CoverageFalse_Rejected()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageResult = false;

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"j3covf{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await TestHelpers.PostOrderAsync(client, BaseJ3Request());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_J3_ApiFailure_RejectedSafely()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageException = new J3ApiException("IsValidServiceArea", "J3 IsValidServiceArea: HTTP 503.", httpStatus: 503);

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"j3fail{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await TestHelpers.PostOrderAsync(client, BaseJ3Request());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("fake-j3-token");
        text.Should().NotContain("Bearer");
    }

    [Fact]
    public async Task CreateOrder_J3WhileDisabled_Rejected()
    {
        await using var factory = new J3DisabledWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"j3off{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await TestHelpers.PostOrderAsync(client, BaseJ3Request());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        j3Fake.CoverageCallCount.Should().Be(0);
    }

    // ── 16–17. anti-simulação no path real (source) ──────────────

    [Fact]
    public void ShippingOptionsService_Source_NoSimulatedJ3Helpers()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Path.Combine(root, "Esotera.Infrastructure", "Services", "ShippingOptionsService.cs");
        File.Exists(path).Should().BeTrue(path);
        var src = File.ReadAllText(path);

        src.Should().NotContain("IsJ3CepEligible");
        src.Should().NotContain("J3WorkingDays");
        src.Should().NotContain("J3CutoffHour");
        src.Should().NotContain("settings.J3Price");
        src.Should().Contain("TryBuildJ3OptionAsync");
        src.Should().Contain("IsServiceAreaAsync");
        src.Should().Contain("HasValidRealQuoteConfig");
    }

    // ── anti-fallback explícito ──────────────────────────────────

    [Fact]
    public async Task Quote_ApiThrows_EvenWhenSimulatedCepEligible_OmitsJ3()
    {
        // 01310100 está na faixa simulada IsJ3CepEligible — ainda assim sem fallback.
        SimulatedShippingService.IsJ3CepEligible("01310100").Should().BeTrue();

        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageException = new J3ApiException("IsValidServiceArea", "J3 IsValidServiceArea: network error.");

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id == "j3");
        body.Options.Should().Contain(o => o.Id.StartsWith("melhor_"));
    }

    // ── 21–22. PAC / SEDEX regressão junto com J3 ────────────────

    [Fact]
    public async Task Quote_PacSedex_StillAvailable_WhenJ3CoverageFalse()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var j3Fake = ShippingTestHelpers.GetJ3Fake(factory.Services);
        j3Fake.Reset();
        j3Fake.CoverageResult = false;

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().Contain(o => o.Id == ShippingMethod.MelhorEconomico);
        body.Options.Should().Contain(o => o.Id == ShippingMethod.MelhorExpresso);
        body.Options.Should().NotContain(o => o.Id == "j3");
    }

    private static CreateOrderRequest BaseJ3Request() =>
        new(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: true),
            null,
            "j3",
            "pix",
            null,
            null);
}
