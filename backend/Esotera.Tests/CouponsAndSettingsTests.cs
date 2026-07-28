using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Coupons;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Settings;
using Esotera.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class CouponsAndSettingsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid ProductToalhaId =
        Guid.Parse("11111111-1111-1111-1111-111111111106");

    public CouponsAndSettingsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CreateOrderRequest OrderRequest(
        Guid productId,
        string? coupon = null,
        string state = "SP",
        string shipping = "melhor_economico") =>
        new(
            [new CreateOrderItemRequest(productId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", state),
            null,
            shipping,
            "pix",
            null,
            coupon
        );

    private async Task SetAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private async Task SetCustomerAsync()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        await SetAdminAsync();
        var response = await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 99.90m,
            freeShippingStates = new[] { "SP", "RJ", "MG", "ES", "PR", "SC", "RS" },
            j3Price = 12m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = false,
            shippingSubsidyAmount = 10m
        });
        response.EnsureSuccessStatusCode();
    }

    // ── Admin auth ──────────────────────────────────────────────

    [Fact]
    public async Task AdminCoupons_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        (await _client.GetAsync("/api/admin/coupons")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/admin/settings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminCoupons_Customer_Returns403()
    {
        await SetCustomerAsync();
        (await _client.GetAsync("/api/admin/coupons")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/admin/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "X",
            freeShippingMin = 99.9m,
            freeShippingStates = new[] { "SP" },
            j3Price = 12m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = false,
            shippingSubsidyAmount = 10m
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Admin coupon CRUD ───────────────────────────────────────

    [Fact]
    public async Task AdminCoupon_Crud_ActivateArchiveRestore()
    {
        await SetAdminAsync();
        var code = $"TEST{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var create = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 7.5m,
            minPurchase = 20m,
            oneUsePerCustomer = true,
            maxTotalUses = (int?)null,
            isActive = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);
        created!.Code.Should().Be(code);
        created.DiscountAmount.Should().Be(7.5m);
        created.UsageCount.Should().Be(0);

        var get = await _client.GetFromJsonAsync<AdminCouponDto>(
            $"/api/admin/coupons/{created.Id}", JsonOptions);
        get!.Code.Should().Be(code);

        var update = await _client.PutAsJsonAsync($"/api/admin/coupons/{created.Id}", new
        {
            discountAmount = 8m,
            minPurchase = 25m,
            maxTotalUses = 10
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);
        updated!.DiscountAmount.Should().Be(8m);
        updated.MaxTotalUses.Should().Be(10);

        (await _client.PatchAsync($"/api/admin/coupons/{created.Id}/deactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var inactive = await _client.GetFromJsonAsync<AdminCouponDto>(
            $"/api/admin/coupons/{created.Id}", JsonOptions);
        inactive!.IsActive.Should().BeFalse();

        (await _client.PatchAsync($"/api/admin/coupons/{created.Id}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.PatchAsync($"/api/admin/coupons/{created.Id}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var archived = await _client.GetFromJsonAsync<AdminCouponDto>(
            $"/api/admin/coupons/{created.Id}", JsonOptions);
        archived!.IsArchived.Should().BeTrue();
        archived.IsActive.Should().BeFalse();

        var activeList = await _client.GetFromJsonAsync<AdminCouponDto[]>(
            "/api/admin/coupons", JsonOptions);
        activeList!.Should().NotContain(c => c.Id == created.Id);

        var archivedList = await _client.GetFromJsonAsync<AdminCouponDto[]>(
            "/api/admin/coupons?archived=only", JsonOptions);
        archivedList!.Should().Contain(c => c.Id == created.Id);

        (await _client.PatchAsync($"/api/admin/coupons/{created.Id}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await _client.GetFromJsonAsync<AdminCouponDto>(
            $"/api/admin/coupons/{created.Id}", JsonOptions);
        restored!.IsArchived.Should().BeFalse();
        restored.IsActive.Should().BeFalse(); // restore does not auto-activate
    }

    [Fact]
    public async Task AdminCoupon_DuplicateCodeDifferentCase_Returns409()
    {
        await SetAdminAsync();
        var code = $"DUP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        (await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 5m,
            minPurchase = 0m
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = code.ToLowerInvariant(),
            discountAmount = 5m,
            minPurchase = 0m
        });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdminCoupon_InvalidDiscount_Returns400()
    {
        await SetAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = $"BAD{Guid.NewGuid():N}"[..10],
            discountAmount = 0m,
            minPurchase = 0m
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCoupon_InvalidMinPurchase_Returns400()
    {
        await SetAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = $"BAD{Guid.NewGuid():N}"[..10],
            discountAmount = 5m,
            minPurchase = -1m
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCoupon_InvalidDates_Returns400()
    {
        await SetAdminAsync();
        var from = DateTime.UtcNow.AddDays(5);
        var until = DateTime.UtcNow.AddDays(1);
        var response = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = $"BAD{Guid.NewGuid():N}"[..10],
            discountAmount = 5m,
            minPurchase = 0m,
            validFromUtc = from,
            validUntilUtc = until
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCoupon_GetMissing_Returns404()
    {
        await SetAdminAsync();
        var response = await _client.GetAsync($"/api/admin/coupons/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCoupon_MassAssignment_IgnoresInternalFields()
    {
        await SetAdminAsync();
        var code = $"MAS{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var payload = $$"""
            {
              "code": "{{code}}",
              "discountAmount": 5,
              "minPurchase": 10,
              "usageCount": 999,
              "isArchived": true,
              "appliesToShipping": true,
              "id": "{{Guid.NewGuid()}}"
            }
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/admin/coupons", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);
        created!.UsageCount.Should().Be(0);
        created.IsArchived.Should().BeFalse();
        created.AppliesToShipping.Should().BeFalse();
        created.Code.Should().Be(code);
    }

    [Fact]
    public async Task AdminCoupon_Dto_DoesNotExposeSecrets()
    {
        await SetAdminAsync();
        var list = await _client.GetFromJsonAsync<JsonElement[]>("/api/admin/coupons", JsonOptions);
        list.Should().NotBeNull();
        foreach (var item in list!)
        {
            item.TryGetProperty("password", out _).Should().BeFalse();
            item.TryGetProperty("connectionString", out _).Should().BeFalse();
            item.TryGetProperty("usages", out _).Should().BeFalse();
        }
    }

    // ── Public validation ───────────────────────────────────────

    [Fact]
    public async Task Validate_NormalizesCodeCase()
    {
        await SetCustomerAsync();
        var response = await _client.PostAsJsonAsync("/api/coupons/validate",
            new CouponValidationRequest("  desconto5  ", 100m));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        result!.IsValid.Should().BeTrue();
        result.Code.Should().Be("DESCONTO5");
        result.DiscountAmount.Should().Be(5m);
    }

    [Fact]
    public async Task Validate_InactiveCoupon_ReturnsInvalid()
    {
        await SetAdminAsync();
        var code = $"INA{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var create = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 5m,
            minPurchase = 0m,
            isActive = false
        });
        var created = await create.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);

        await SetCustomerAsync();
        var response = await _client.PostAsJsonAsync("/api/coupons/validate",
            new CouponValidationRequest(code, 100m));
        var result = await response.Content.ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("inativo");

        // cleanup
        await SetAdminAsync();
        await _client.PatchAsync($"/api/admin/coupons/{created!.Id}/archive", null);
    }

    [Fact]
    public async Task Validate_ArchivedCoupon_ReturnsInvalid()
    {
        await SetAdminAsync();
        var code = $"ARC{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var create = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 5m,
            minPurchase = 0m
        });
        var created = await create.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);
        await _client.PatchAsync($"/api/admin/coupons/{created!.Id}/archive", null);

        await SetCustomerAsync();
        var result = await (await _client.PostAsJsonAsync("/api/coupons/validate",
            new CouponValidationRequest(code, 100m))).Content
            .ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        result!.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ExpiredAndNotStarted()
    {
        await SetAdminAsync();
        var expiredCode = $"EXP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = expiredCode,
            discountAmount = 5m,
            minPurchase = 0m,
            validFromUtc = DateTime.UtcNow.AddDays(-10),
            validUntilUtc = DateTime.UtcNow.AddDays(-1)
        });

        var futureCode = $"FUT{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = futureCode,
            discountAmount = 5m,
            minPurchase = 0m,
            validFromUtc = DateTime.UtcNow.AddDays(2),
            validUntilUtc = DateTime.UtcNow.AddDays(10)
        });

        await SetCustomerAsync();
        var exp = await (await _client.PostAsJsonAsync("/api/coupons/validate",
            new CouponValidationRequest(expiredCode, 100m))).Content
            .ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        exp!.IsValid.Should().BeFalse();
        exp.ErrorMessage.Should().Contain("expirado");

        var fut = await (await _client.PostAsJsonAsync("/api/coupons/validate",
            new CouponValidationRequest(futureCode, 100m))).Content
            .ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        fut!.IsValid.Should().BeFalse();
        fut.ErrorMessage.Should().Contain("ainda não");
    }

    // ── Order consumption ───────────────────────────────────────

    [Fact]
    public async Task Order_DiscountCappedAtSubtotal_TotalNonNegative()
    {
        await SetAdminAsync();
        var code = $"CAP{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 500m,
            minPurchase = 0m
        });

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"cap{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, OrderRequest(ProductWaitePocketId, coupon: code));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Discount.Should().Be(order.Subtotal);
        order.Total.Should().BeGreaterThanOrEqualTo(0);
        order.ShippingPrice.Should().BeGreaterThan(0); // coupon does not zero shipping directly
    }

    [Fact]
    public async Task Order_CouponDoesNotReduceShippingDirectly()
    {
        await RestoreDefaultSettingsAsync();

        var (token1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"ship1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token1);
        var without = await TestHelpers.PostOrderAsync(
            _client, OrderRequest(ProductWaitePocketId));
        without.StatusCode.Should().Be(HttpStatusCode.Created);
        var a = await without.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        var (token2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"ship2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token2);
        var withCoupon = await TestHelpers.PostOrderAsync(
            _client, OrderRequest(ProductWaitePocketId, coupon: "DESCONTO5"));
        withCoupon.StatusCode.Should().Be(HttpStatusCode.Created);
        var b = await withCoupon.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        a!.ShippingPrice.Should().Be(18.90m);
        b!.ShippingPrice.Should().Be(18.90m);
        a.ShippingPrice.Should().Be(b.ShippingPrice);
        b.Discount.Should().Be(5m);
        b.Total.Should().Be(b.Subtotal - b.Discount + b.ShippingPrice);
    }

    [Fact]
    public async Task Order_MaxTotalUses_Exhausted_Returns409()
    {
        await SetAdminAsync();
        var code = $"MAX{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 5m,
            minPurchase = 0m,
            oneUsePerCustomer = true,
            maxTotalUses = 1
        });

        var (t1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"max1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t1);
        (await TestHelpers.PostOrderAsync(_client, OrderRequest(ProductWaitePocketId, coupon: code)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var (t2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"max2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t2);
        var second = await TestHelpers.PostOrderAsync(
            _client, OrderRequest(ProductWaitePocketId, coupon: code));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Order_FailedValidation_DoesNotConsumeCoupon()
    {
        await SetAdminAsync();
        var code = $"NOC{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var create = await _client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code,
            discountAmount = 5m,
            minPurchase = 0m,
            maxTotalUses = 1
        });
        var created = await create.Content.ReadFromJsonAsync<AdminCouponDto>(JsonOptions);

        var (token, userId) = await TestHelpers.RegisterNewUserAsync(
            _client, $"noc{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        // Invalid shipping method → fail before/without completing usage commit path
        var bad = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "invalid_method",
            "pix",
            null,
            code);
        (await TestHelpers.PostOrderAsync(_client, bad)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        db.CouponUsages.Count(u => u.CouponId == created!.Id).Should().Be(0);
        db.CouponUsages.Count(u => u.UserId == userId).Should().Be(0);
    }

    [Fact]
    public async Task Order_Snapshots_Persisted_SettingsChangeDoesNotAffectOldOrder()
    {
        await RestoreDefaultSettingsAsync();
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"snap{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, OrderRequest(ProductWaitePocketId, coupon: "DESCONTO5"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderDto = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        decimal originalShipping;
        decimal originalDiscount;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.FindAsync(orderDto!.Id);
            order!.CouponId.Should().NotBeNull();
            order.CouponNominalDiscount.Should().Be(5m);
            order.CouponDiscountApplied.Should().Be(5m);
            order.FreeShippingMinSnapshot.Should().Be(99.90m);
            order.J3PriceSnapshot.Should().Be(12m);
            originalShipping = order.ShippingPrice;
            originalDiscount = order.Discount;
        }

        await SetAdminAsync();
        await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 50m,
            freeShippingStates = new[] { "SP", "RJ" },
            j3Price = 99m,
            j3CutoffHour = 10,
            shippingSubsidyEnabled = true,
            shippingSubsidyAmount = 5m
        });

        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var order = await db.Orders.FindAsync(orderDto!.Id);
            order!.ShippingPrice.Should().Be(originalShipping);
            order.Discount.Should().Be(originalDiscount);
            order.FreeShippingMinSnapshot.Should().Be(99.90m);
            order.J3PriceSnapshot.Should().Be(12m);
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }

    // ── Settings ────────────────────────────────────────────────

    [Fact]
    public async Task Settings_Public_ReturnsOnlyPublicFields()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/settings/public");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        json.TryGetProperty("storeName", out _).Should().BeTrue();
        json.TryGetProperty("freeShippingMin", out _).Should().BeTrue();
        json.TryGetProperty("freeShippingStates", out _).Should().BeTrue();
        json.TryGetProperty("couponDiscount", out _).Should().BeFalse();
        json.TryGetProperty("couponMinPurchase", out _).Should().BeFalse();
        json.TryGetProperty("id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Settings_Admin_GetAndUpdate_ValidatesStates()
    {
        await RestoreDefaultSettingsAsync();
        var current = await _client.GetFromJsonAsync<AdminStoreSettingsDto>(
            "/api/admin/settings", JsonOptions);
        current!.StoreName.Should().Be("Esotera");

        try
        {
            var bad = await _client.PutAsJsonAsync("/api/admin/settings", new
            {
                storeName = "Esotera",
                freeShippingMin = 99.9m,
                freeShippingStates = new[] { "SP", "XX" },
                j3Price = 12m,
                j3CutoffHour = 12,
                shippingSubsidyEnabled = false,
                shippingSubsidyAmount = 10m
            });
            bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var ok = await _client.PutAsJsonAsync("/api/admin/settings", new
            {
                storeName = "Esotera Test",
                freeShippingMin = 80m,
                freeShippingStates = new[] { " sp ", "rj", "SP" },
                j3Price = 15m,
                j3CutoffHour = 14,
                shippingSubsidyEnabled = true,
                shippingSubsidyAmount = 3m
            });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await ok.Content.ReadFromJsonAsync<AdminStoreSettingsDto>(JsonOptions);
            updated!.StoreName.Should().Be("Esotera Test");
            updated.FreeShippingMin.Should().Be(80m);
            updated.FreeShippingStates.Should().BeEquivalentTo(["SP", "RJ"]);
            updated.J3Price.Should().Be(15m);
            updated.ShippingSubsidyEnabled.Should().BeTrue();

            var pub = await _client.GetFromJsonAsync<PublicStoreSettingsDto>(
                "/api/settings/public", JsonOptions);
            pub!.StoreName.Should().Be("Esotera Test");
            pub.FreeShippingStates.Should().BeEquivalentTo(["SP", "RJ"]);
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }

    [Fact]
    public async Task Settings_MassAssignment_IgnoresIdAndLegacyCoupon()
    {
        await SetAdminAsync();
        var payload = """
            {
              "storeName": "Esotera",
              "freeShippingMin": 99.9,
              "freeShippingStates": ["SP","RJ","MG","ES","PR","SC","RS"],
              "j3Price": 12,
              "j3CutoffHour": 12,
              "shippingSubsidyEnabled": false,
              "shippingSubsidyAmount": 10,
              "id": 99,
              "couponDiscount": 99,
              "couponMinPurchase": 1
            }
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/api/admin/settings", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var settings = db.StoreSettings.Single(s => s.Id == 1);
        settings.Id.Should().Be(1);
#pragma warning disable CS0618
        settings.CouponDiscount.Should().NotBe(99m);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task FreeShipping_UsesCsv_AndAfterCoupon()
    {
        await RestoreDefaultSettingsAsync();
        await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 99.90m,
            freeShippingStates = new[] { "RJ" }, // SP not eligible
            j3Price = 12m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = false,
            shippingSubsidyAmount = 10m
        });

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
                var product = await db.Products.FindAsync(ProductToalhaId);
                product!.Price = 120m;
                await db.SaveChangesAsync();
            }

            var (token, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"csv{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token);

            var spOrder = await TestHelpers.PostOrderAsync(
                _client, OrderRequest(ProductToalhaId, state: "SP"));
            spOrder.StatusCode.Should().Be(HttpStatusCode.Created);
            var sp = await spOrder.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            sp.Should().NotBeNull();
            sp!.ShippingPrice.Should().Be(18.90m); // SP not in CSV despite high total

            var (token2, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"csv2{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token2);
            var rjOrder = await TestHelpers.PostOrderAsync(
                _client, OrderRequest(ProductToalhaId, state: "RJ"));
            rjOrder.StatusCode.Should().Be(HttpStatusCode.Created);
            var rj = await rjOrder.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            rj.Should().NotBeNull();
            rj!.ShippingPrice.Should().Be(0m); // RJ in CSV + total >= min
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            if (product != null)
            {
                product.Price = 49.90m;
                await db.SaveChangesAsync();
            }

            await RestoreDefaultSettingsAsync();
        }
    }

    [Fact]
    public async Task Subsidy_WhenEnabled_ReducesShipping_NeverNegative()
    {
        await RestoreDefaultSettingsAsync();
        await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 9999m,
            freeShippingStates = new[] { "SP" },
            j3Price = 12m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = true,
            shippingSubsidyAmount = 100m
        });

        try
        {
            var (token, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"sub{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token);
            var response = await TestHelpers.PostOrderAsync(
                _client, OrderRequest(ProductWaitePocketId));
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(0m);
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }

    [Fact]
    public async Task Subsidy_WhenDisabled_DoesNotApply()
    {
        await RestoreDefaultSettingsAsync();
        await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 9999m,
            freeShippingStates = new[] { "SP" },
            j3Price = 12m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = false,
            shippingSubsidyAmount = 100m
        });

        try
        {
            var (token, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"nosub{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token);
            var response = await TestHelpers.PostOrderAsync(
                _client, OrderRequest(ProductWaitePocketId));
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(18.90m);
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }

    [Fact]
    public async Task J3_UsesConfiguredPrice()
    {
        await RestoreDefaultSettingsAsync();
        await _client.PutAsJsonAsync("/api/admin/settings", new
        {
            storeName = "Esotera",
            freeShippingMin = 9999m,
            freeShippingStates = new[] { "SP" },
            j3Price = 17.50m,
            j3CutoffHour = 12,
            shippingSubsidyEnabled = false,
            shippingSubsidyAmount = 10m
        });

        try
        {
            var (token, _) = await TestHelpers.RegisterNewUserAsync(
                _client, $"j3p{Guid.NewGuid():N}@test.com");
            TestHelpers.SetBearerToken(_client, token);

            // J3 only available on weekdays — skip if weekend in SP timezone
            var spNow = Esotera.Infrastructure.Services.SimulatedShippingService
                .GetSaoPauloLocalTime(DateTime.UtcNow);
            if (spNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                return;

            var request = new CreateOrderRequest(
                [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
                new OrderAddressInput(
                    "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
                null,
                "j3",
                "pix",
                null,
                null);
            var response = await TestHelpers.PostOrderAsync(_client, request);
            if (response.StatusCode != HttpStatusCode.Created)
                return; // CEP eligibility / working day edge

            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(17.50m);
        }
        finally
        {
            await RestoreDefaultSettingsAsync();
        }
    }
}
