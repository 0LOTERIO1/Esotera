using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Shipping;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class MelhorEnvioQuoteTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    // ── Mapper ──────────────────────────────────────────────────

    [Fact]
    public void Mapper_MapsPacAndSedex_ByCompanyAndServiceIds()
    {
        MelhorEnvioQuoteMapper.MapToShippingMethodId(1, 1).Should().Be(ShippingMethod.MelhorEconomico);
        MelhorEnvioQuoteMapper.MapToShippingMethodId(1, 2).Should().Be(ShippingMethod.MelhorExpresso);
        MelhorEnvioQuoteMapper.MapToShippingMethodId(1, 3).Should().BeNull();
        MelhorEnvioQuoteMapper.MapToShippingMethodId(2, 1).Should().BeNull();
    }

    [Fact]
    public void Mapper_PrefersCustomPriceAndCustomDeliveryTime()
    {
        var raw = new MelhorEnvioRawServiceQuote
        {
            CompanyId = 1,
            CompanyName = "Correios",
            ServiceId = 1,
            ServiceName = "PAC",
            Price = 99m,
            CustomPrice = 18.90m,
            DeliveryTime = 99,
            CustomDeliveryTime = 5
        };

        var mapped = MelhorEnvioQuoteMapper.TryMapService(raw, DateTime.UtcNow, "sandbox");
        mapped.Should().NotBeNull();
        mapped!.OriginalPrice.Should().Be(18.90m);
        mapped.EstimatedDaysMin.Should().Be(5);
        mapped.ShippingMethodId.Should().Be(ShippingMethod.MelhorEconomico);
    }

    [Fact]
    public void Mapper_MissingPriceOrDelivery_ReturnsNull()
    {
        MelhorEnvioQuoteMapper.TryMapService(new MelhorEnvioRawServiceQuote
        {
            CompanyId = 1,
            ServiceId = 1,
            DeliveryTime = 3
        }, DateTime.UtcNow, "sandbox").Should().BeNull();

        MelhorEnvioQuoteMapper.TryMapService(new MelhorEnvioRawServiceQuote
        {
            CompanyId = 1,
            ServiceId = 2,
            Price = 20m
        }, DateTime.UtcNow, "sandbox").Should().BeNull();

        MelhorEnvioQuoteMapper.TryMapService(new MelhorEnvioRawServiceQuote
        {
            CompanyId = 1,
            ServiceId = 1,
            Price = 10m,
            DeliveryTime = 2,
            Error = "CEP inválido"
        }, DateTime.UtcNow, "sandbox").Should().BeNull();
    }

    [Fact]
    public void NoMelhorEnvioAccessToken_InSourceTree()
    {
        var forbidden = string.Join("_", ["MELHOR", "ENVIO", "ACCESS", "TOKEN"]);
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var hits = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith('.') && name != ".env.example") return false;
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".cs" or ".ts" or ".tsx" or ".md" or ".example" or ".json" or ".env" or "";
            })
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.next{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("MelhorEnvioQuoteTests.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                try
                {
                    return File.ReadAllText(f).Contains(forbidden, StringComparison.Ordinal);
                }
                catch
                {
                    return false;
                }
            })
            .ToList();

        // Excluir o próprio arquivo de teste da lista (já filtrado acima); hits deve ficar vazio.
        hits.Should().BeEmpty("auth Melhor Envio é somente OAuth — sem token estático em env");
    }

    // ── HTTP quote endpoint ─────────────────────────────────────

    [Fact]
    public async Task Quote_200_ReturnsNormalizedOptions_WithoutRawMe()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310-100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Ok.Should().BeTrue();
        body.Options.Should().Contain(o => o.Id == "melhor_economico");
        body.Options.Should().Contain(o => o.Id == "melhor_expresso");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("fake-access");
        raw.Should().NotContain("custom_price");
        raw.Should().NotContain("Bearer");
    }

    [Fact]
    public async Task Quote_TimeoutOrNetwork_NoSimulatedMe_J3MayRemain()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.TimedOut = true;

        var spNow = SimulatedShippingService.GetSaoPauloLocalTime(DateTime.UtcNow);
        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id.StartsWith("melhor_"));
        if (spNow.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            body.Options.Should().Contain(o => o.Id == "j3");
    }

    [Fact]
    public async Task Quote_401_RefreshesOnce_ThenSucceeds()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.ReturnUnauthenticatedOnce = true;

        // Access token próximo do vencimento força refresh no GetValidAccessToken;
        // aqui o 401 no calculate dispara ExecuteWithTokenRetryAsync.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var conn = await db.MelhorEnvioConnections.FirstAsync();
            conn.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddDays(20);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Ok.Should().BeTrue();
        fake.CallCount.Should().Be(2);
        fake.AccessTokensUsed.Count.Should().Be(2);
    }

    [Fact]
    public async Task Quote_TokenUnavailable_NoMeOptions()
    {
        await using var factory = new CustomWebApplicationFactory();
        await ShippingTestHelpers.ClearOAuthConnectionsAsync(factory.Services);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id.StartsWith("melhor_"));
    }

    [Fact]
    public async Task Quote_OAuthDisconnected_NoMeOptions()
    {
        await using var factory = new CustomWebApplicationFactory();
        await ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(factory.Services, enabled: true, withOAuthConnection: false);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id.StartsWith("melhor_"));
    }

    [Fact]
    public async Task Quote_FlagOff_NoMeOptions_EvenWithOAuth()
    {
        await using var factory = new CustomWebApplicationFactory();
        await ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(factory.Services, enabled: false, withOAuthConnection: true);
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().NotContain(o => o.Id.StartsWith("melhor_"));
        fake.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Quote_InvalidCep_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "123",
            state = "SP",
            productsSubtotal = 40m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Ok.Should().BeFalse();
        body.ErrorCode.Should().Be("invalid_cep");
    }

    [Fact]
    public async Task Quote_ServiceMissing_OptionOmitted()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.CustomServices = _ =>
        [
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1,
                ServiceId = 1,
                ServiceName = "PAC",
                Price = 18.90m,
                DeliveryTime = 5
            }
            // SEDEX ausente
        ];

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().Contain(o => o.Id == "melhor_economico");
        body.Options.Should().NotContain(o => o.Id == "melhor_expresso");
    }

    [Fact]
    public async Task Quote_FreeShipping_PreservesOriginalPrice()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 150m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        var eco = body!.Options.Single(o => o.Id == "melhor_economico");
        eco.Price.Should().Be(0m);
        eco.OriginalPrice.Should().Be(18.90m);
        eco.FreeShippingApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Quote_J3Regression_IndependentOfMeFailure()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.NetworkError = true;

        var spNow = SimulatedShippingService.GetSaoPauloLocalTime(DateTime.UtcNow);
        if (spNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return;

        var response = await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 50m
        });

        var body = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions);
        body!.Options.Should().Contain(o => o.Id == "j3");
        body.Options.Should().NotContain(o => o.Id.StartsWith("melhor_"));
    }

    // ── CreateOrder ─────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_IgnoresFrontendShippingPrice_UsesRecalc()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.CustomServices = _ =>
        [
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1, ServiceId = 1, ServiceName = "PAC",
                Price = 22.50m, DeliveryTime = 4
            },
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1, ServiceId = 2, ServiceName = "SEDEX",
                Price = 33m, DeliveryTime = 2
            }
        ];

        var (token, _) = await TestHelpers.RegisterNewUserAsync(client, $"meprice{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        // Payload sem campo de preço de frete — API só aceita shippingMethodId.
        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.ShippingPrice.Should().Be(22.50m);
        fake.CallCount.Should().BeGreaterThan(0);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var entity = await db.Orders.FindAsync(order.Id);
        entity!.ShippingOriginalPrice.Should().Be(22.50m);
        entity.ShippingCompanyId.Should().Be(1);
        entity.ShippingServiceId.Should().Be(1);
        entity.ShippingQuoteEnvironment.Should().Be("sandbox");
        entity.ShippingQuotedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateOrder_Recalc_MethodGone_Blocked()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.CustomServices = _ =>
        [
            new MelhorEnvioRawServiceQuote
            {
                CompanyId = 1, ServiceId = 2, ServiceName = "SEDEX",
                Price = 28.90m, DeliveryTime = 2
            }
            // PAC ausente → melhor_economico indisponível
        ];

        var (token, _) = await TestHelpers.RegisterNewUserAsync(client, $"megone{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_FlagOff_BlocksMeMethod()
    {
        await using var factory = new CustomWebApplicationFactory();
        await ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(factory.Services, enabled: false, withOAuthConnection: true);
        var client = factory.CreateClient();

        var (token, _) = await TestHelpers.RegisterNewUserAsync(client, $"meoff{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_FreeShipping_StoresOriginalPrice()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductWaitePocketId);
            product!.Price = 120m;
            await db.SaveChangesAsync();
        }

        try
        {
            var (token, _) = await TestHelpers.RegisterNewUserAsync(client, $"mefree{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(client, token);

            var request = new CreateOrderRequest(
                [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
                new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
                null,
                "melhor_economico",
                "pix",
                null,
                null);

            var response = await TestHelpers.PostOrderAsync(client, request);
            response.EnsureSuccessStatusCode();
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(0m);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Orders.FindAsync(order.Id);
            entity!.ShippingOriginalPrice.Should().Be(18.90m);
            entity.ShippingFreeShippingApplied.Should().BeTrue();
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductWaitePocketId);
            if (product != null)
            {
                product.Price = 79.90m;
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task CreateOrder_NoSimulatedMeFallback_WhenMeFails()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();
        fake.FailOk = true;

        var (token, _) = await TestHelpers.RegisterNewUserAsync(client, $"mefail{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Quote_UsesPackageDefaults_16x11x6_400g()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var fake = ShippingTestHelpers.GetShipmentFake(factory.Services);
        fake.Reset();

        await client.PostAsJsonAsync("/api/shipping/quote", new
        {
            destinationCep = "01310100",
            state = "SP",
            productsSubtotal = 40m
        });

        fake.LastRequest.Should().NotBeNull();
        fake.LastRequest!.LengthCm.Should().Be(16m);
        fake.LastRequest.WidthCm.Should().Be(11m);
        fake.LastRequest.HeightCm.Should().Be(6m);
        fake.LastRequest.WeightKg.Should().Be(0.4m);
        fake.LastRequest.FromPostalCode.Should().Be("08061-420");
        fake.LastRequest.Services.Should().Be("1,2");
    }
}
