using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Addresses;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class OrderIdempotencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaiteTradId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid ProductWaitePocketId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid ProductCrowleyId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    private static readonly Guid ProductToalhaId = Guid.Parse("11111111-1111-1111-1111-111111111106");

    public OrderIdempotencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CreateOrderRequest BaseRequest(
        Guid productId,
        int qty = 1,
        string shipping = "melhor_economico",
        string payment = "pix",
        string? coupon = null,
        OrderAddressInput? address = null) =>
        new(
            [new CreateOrderItemRequest(productId, qty, null)],
            address ?? new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            shipping,
            payment,
            payment == "card" ? 1 : null,
            coupon
        );

    [Fact]
    public async Task IdempotentReplay_SameKeySamePayload_ReturnsSameOrder()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var key = Guid.NewGuid().ToString();
        var request = BaseRequest(ProductWaitePocketId);

        var r1 = await TestHelpers.PostOrderAsync(_client, request, key);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);
        var o1 = await r1.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        var r2 = await TestHelpers.PostOrderAsync(_client, request, key);
        r2.StatusCode.Should().Be(HttpStatusCode.Created);
        var o2 = await r2.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        o2!.Id.Should().Be(o1!.Id);
        o2.OrderNumber.Should().Be(o1.OrderNumber);

        var list = await _client.GetFromJsonAsync<OrderListDto[]>("/api/orders", JsonOptions);
        list!.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConcurrentSameKey_CreatesOnlyOneOrder()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var key = Guid.NewGuid().ToString();
        var request = BaseRequest(ProductWaitePocketId);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => TestHelpers.PostOrderAsync(_client, request, key));
        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var ids = new List<Guid>();
        foreach (var r in responses)
        {
            var o = await r.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            ids.Add(o!.Id);
        }

        ids.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task SameKeyDifferentPayload_ReturnsConflict()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem3{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var key = Guid.NewGuid().ToString();

        var r1 = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId), key);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        var r2 = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductCrowleyId), key);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DifferentUsers_SameKey_DoNotConflict()
    {
        var key = Guid.NewGuid().ToString();
        var (t1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem4a{Guid.NewGuid():N}@test.com");
        var (t2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem4b{Guid.NewGuid():N}@test.com");

        TestHelpers.SetBearerToken(_client, t1);
        var r1 = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId), key);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        TestHelpers.SetBearerToken(_client, t2);
        var r2 = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId), key);
        r2.StatusCode.Should().Be(HttpStatusCode.Created);

        var o1 = await r1.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        var o2 = await r2.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        o1!.Id.Should().NotBe(o2!.Id);
    }

    [Fact]
    public async Task MissingIdempotencyKey_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem5{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await _client.PostAsJsonAsync(
            "/api/orders", BaseRequest(ProductWaitePocketId));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyIdempotencyKey_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem6{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId), "   ");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OversizedIdempotencyKey_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"idem7{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId), new string('a', 65));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddressOfAnotherUser_ReturnsNotFound()
    {
        var (t1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"addr1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t1);

        var createAddr = await _client.PostAsJsonAsync("/api/users/me/addresses", new CreateAddressRequest(
            "01310100", "Rua A", "10", null, "Bela Vista", "São Paulo", "SP", true));
        createAddr.StatusCode.Should().Be(HttpStatusCode.Created);
        var addr = await createAddr.Content.ReadFromJsonAsync<AddressDto>(JsonOptions);

        var (t2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"addr2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t2);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            null,
            addr!.Id,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonexistentProduct_ReturnsNotFound()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"noprod{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnavailableProduct_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"unavail{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductCrowleyId);
            product!.IsAvailable = false;
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await TestHelpers.PostOrderAsync(
                _client, BaseRequest(ProductCrowleyId));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductCrowleyId);
            product!.IsAvailable = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task InvalidQuantity_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"qty{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 0, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FrontendPricesDoNotControlTotal()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"price{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Subtotal.Should().Be(59.90m);
        order.Items[0].UnitPrice.Should().Be(59.90m);
        order.ShippingPrice.Should().Be(18.90m);
        order.Total.Should().Be(78.80m);
    }

    [Fact]
    public async Task ValidCoupon_AppliesDiscount()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"cupomok{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId, coupon: "DESCONTO5"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Discount.Should().Be(5m);
        order.Total.Should().Be(59.90m - 5m + 18.90m);
    }

    [Fact]
    public async Task CouponBelowMinimum_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"cupommin{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var coupon = db.Coupons.First(c => c.Code == "DESCONTO5");
            coupon.MinPurchase = 200m;
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await TestHelpers.PostOrderAsync(
                _client, BaseRequest(ProductWaitePocketId, coupon: "DESCONTO5"));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var coupon = db.Coupons.First(c => c.Code == "DESCONTO5");
            coupon.MinPurchase = 30m;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task CouponAlreadyUsed_ReturnsBadRequest()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"cupomused{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var first = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId, coupon: "DESCONTO5"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductCrowleyId, coupon: "DESCONTO5"));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IdempotentRetry_DoesNotConsumeCouponTwice()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"cupomidem{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var key = Guid.NewGuid().ToString();
        var request = BaseRequest(ProductWaitePocketId, coupon: "DESCONTO5");

        (await TestHelpers.PostOrderAsync(_client, request, key)).StatusCode
            .Should().Be(HttpStatusCode.Created);
        (await TestHelpers.PostOrderAsync(_client, request, key)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var list = await _client.GetFromJsonAsync<OrderListDto[]>("/api/orders", JsonOptions);
        list.Should().HaveCount(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var userId = (await db.Users.FirstAsync(u => u.Email.StartsWith("cupomidem"))).Id;
        db.CouponUsages.Count(u => u.UserId == userId).Should().Be(1);
    }

    [Fact]
    public async Task FreeShipping_AtExactMinimum()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"free99{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 99.90m;
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await TestHelpers.PostOrderAsync(
                _client, BaseRequest(ProductToalhaId));
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(0);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 49.90m;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task FreeShipping_BelowMinimum_NotApplied()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"free989{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 99.89m;
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await TestHelpers.PostOrderAsync(
                _client, BaseRequest(ProductToalhaId));
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.ShippingPrice.Should().Be(18.90m);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 49.90m;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task FreeShipping_RemovedByCoupon_104_to_99()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"freecupom{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 104.00m;
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await TestHelpers.PostOrderAsync(
                _client, BaseRequest(ProductToalhaId, coupon: "DESCONTO5"));
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.Discount.Should().Be(5m);
            (order.Subtotal - order.Discount).Should().Be(99.00m);
            order.ShippingPrice.Should().Be(18.90m);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var product = await db.Products.FindAsync(ProductToalhaId);
            product!.Price = 49.90m;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task OutsideSouthSoutheast_NoFreeShipping()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"norte{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 2, null)],
            new OrderAddressInput("69000000", "Av Brasil", "100", null, "Centro", "Manaus", "AM"),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.ShippingPrice.Should().Be(39.90m);
    }

    [Fact]
    public async Task J3_EligibleCep_WeekdayBeforeCutoff()
    {
        var monday10Utc = new DateTime(2026, 7, 27, 13, 0, 0, DateTimeKind.Utc);
        var shipping = new SimulatedShippingService(new FixedClock(monday10Utc));
        var settings = new StoreSettings
        {
            FreeShippingMin = 99.90m,
            J3Price = 12m,
            J3CutoffHour = 12,
            ShippingSubsidyEnabled = false
        };

        var (price, days) = shipping.Quote("j3", "01310100", "SP", 50m, settings);
        price.Should().Be(12m);
        days.Should().Be(0);

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"j3ok{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        // Integração: se o relógio real for dia útil, deve criar; senão 400.
        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId, shipping: "j3"));
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
            order!.Shipping.MethodId.Should().Be("j3");
        }
    }

    [Fact]
    public async Task J3_IneligibleCep_Rejected()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"j3bad{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("70000000", "SQN", "1", null, "Asa Norte", "Brasília", "DF"),
            null,
            "j3",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidShippingMethod_Rejected()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"shipbad{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "teletransporte",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubsidyDisabled_NotApplied()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"subsidy{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var settings = db.StoreSettings.First();
            settings.ShippingSubsidyEnabled = false;
            settings.ShippingSubsidyAmount = 10m;
            await db.SaveChangesAsync();
        }

        var response = await TestHelpers.PostOrderAsync(
            _client, BaseRequest(ProductWaitePocketId));
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.ShippingPrice.Should().Be(18.90m);
    }

    [Fact]
    public async Task FrozenAddress_SurvivesAccountAddressEdit()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"freeze{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var createAddr = await _client.PostAsJsonAsync("/api/users/me/addresses", new CreateAddressRequest(
            "01310100", "Rua Original", "10", null, "Bela Vista", "São Paulo", "SP", true));
        var addr = await createAddr.Content.ReadFromJsonAsync<AddressDto>(JsonOptions);

        var orderReq = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            null,
            addr!.Id,
            "melhor_economico",
            "pix",
            null,
            null);

        var createOrder = await TestHelpers.PostOrderAsync(_client, orderReq);
        var order = await createOrder.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        await _client.PutAsJsonAsync($"/api/users/me/addresses/{addr.Id}", new UpdateAddressRequest(
            "01310100", "Rua Alterada", "99", null, "Bela Vista", "São Paulo", "SP", true));

        var get = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order!.Id}", JsonOptions);
        get!.Address.Street.Should().Be("Rua Original");
        get.Address.Number.Should().Be("10");
    }

    [Fact]
    public async Task ListOrders_OnlyOwn()
    {
        var (t1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"list1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t1);
        await TestHelpers.PostOrderAsync(_client, BaseRequest(ProductWaitePocketId));

        var (t2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"list2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t2);
        await TestHelpers.PostOrderAsync(_client, BaseRequest(ProductCrowleyId));

        var list = await _client.GetFromJsonAsync<OrderListDto[]>("/api/orders", JsonOptions);
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrder_OtherUser_ReturnsNotFound()
    {
        var (t1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"get1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t1);
        var created = await TestHelpers.PostOrderAsync(_client, BaseRequest(ProductWaitePocketId));
        var order = await created.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        var (t2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"get2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, t2);

        var response = await _client.GetAsync($"/api/orders/{order!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void CreateOrderRequest_HasNoCardSensitiveFields()
    {
        var props = typeof(CreateOrderRequest).GetProperties().Select(p => p.Name).ToHashSet();
        props.Should().NotContain("CardNumber");
        props.Should().NotContain("Cvv");
        props.Should().NotContain("CardHolder");
        props.Should().NotContain("Expiry");
        props.Should().Contain("PaymentMethod");
        props.Should().Contain("Installments");
    }

    [Fact]
    public void J3_Weekend_Rejected_Deterministic()
    {
        var saturdayUtc = new DateTime(2026, 7, 25, 15, 0, 0, DateTimeKind.Utc);
        var shipping = new SimulatedShippingService(new FixedClock(saturdayUtc));
        var settings = new StoreSettings { J3Price = 12m, J3CutoffHour = 12 };

        var act = () => shipping.Quote("j3", "01310100", "SP", 50m, settings);
        act.Should().Throw<Application.Exceptions.ValidationException>();
    }
}

file sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}
