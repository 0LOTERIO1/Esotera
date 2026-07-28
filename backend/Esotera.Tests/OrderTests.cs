using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using FluentAssertions;

namespace Esotera.Tests;

public class OrderTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaiteTradId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid ProductWaitePocketId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public OrderTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateOrderRequest ValidRequest(
        Guid productId,
        string shipping = "melhor_economico",
        string payment = "pix",
        int? installments = null,
        string? coupon = null) =>
        new(
            [new CreateOrderItemRequest(productId, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            shipping,
            payment,
            installments,
            coupon
        );

    [Fact]
    public async Task UnavailableProductCannotBeOrdered()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"ordertest1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var adminToken = await TestHelpers.GetAdminTokenAsync(_client);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductWaiteTradId}/availability",
            new { IsAvailable = false });

        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client, ValidRequest(ProductWaiteTradId, shipping: "melhor_economico"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductWaiteTradId}/availability",
            new { IsAvailable = true });
    }

    [Fact]
    public async Task CouponAlreadyUsed_ReturnsError()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"coupontest{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = ValidRequest(ProductWaitePocketId, shipping: "melhor_economico", coupon: "DESCONTO5");

        var response1 = await TestHelpers.PostOrderAsync(_client, request);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        var response2 = await TestHelpers.PostOrderAsync(_client, request);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OrderTotalCalculation_IsCorrect()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"totaltest{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [
                new CreateOrderItemRequest(ProductWaiteTradId, 2, null),
                new CreateOrderItemRequest(ProductWaitePocketId, 1, null)
            ],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null
        );

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order.Should().NotBeNull();
        order!.Subtotal.Should().Be(89.90m * 2 + 59.90m);
        // ≥ 99.90 após desconto → frete grátis Sul/Sudeste
        order.ShippingPrice.Should().Be(0);
        order.Total.Should().Be(order.Subtotal);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsOrder()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"createtest{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = ValidRequest(
            ProductWaiteTradId,
            shipping: "melhor_economico",
            payment: "card",
            installments: 2);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order.Should().NotBeNull();
        order!.OrderNumber.Should().StartWith("ES");
        order.Status.Should().Be("awaiting_payment");
        order.Items.Should().HaveCount(1);
        order.Payment.Installments.Should().Be(2);
    }

    [Fact]
    public async Task CustomerOnlySeesOwnOrders()
    {
        var (token1, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"user1{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token1);

        var createResponse = await TestHelpers.PostOrderAsync(
            _client, ValidRequest(ProductWaiteTradId, shipping: "melhor_economico"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        var (token2, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"user2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token2);

        var getResponse = await _client.GetAsync($"/api/orders/{createdOrder!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCanUpdateOrderStatus()
    {
        var (customerToken, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"statustest{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, customerToken);

        var adminToken = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, adminToken);
        await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductWaiteTradId}/availability",
            new { isAvailable = true });

        TestHelpers.SetBearerToken(_client, customerToken);

        var createResponse = await TestHelpers.PostOrderAsync(
            _client, ValidRequest(ProductWaiteTradId, shipping: "melhor_economico"));
        createResponse.StatusCode.Should().Be(
            HttpStatusCode.Created,
            await createResponse.Content.ReadAsStringAsync());
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order.Should().NotBeNull();

        TestHelpers.SetBearerToken(_client, adminToken);
        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{order!.Id}/status",
            new { status = "preparing", note = "Pedido em preparação" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updatedOrder = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        updatedOrder!.Status.Should().Be("preparing");
        updatedOrder.StatusHistory.Should().HaveCountGreaterThan(1);
    }
}
