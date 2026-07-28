using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.Orders;
using FluentAssertions;

namespace Esotera.Tests;

public class AdminPanelTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid ProductCrowleyId =
        Guid.Parse("11111111-1111-1111-1111-111111111103");

    public AdminPanelTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateOrderRequest OrderRequest(
        Guid productId,
        int qty = 1,
        string payment = "pix") =>
        new(
            [new CreateOrderItemRequest(productId, qty, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            payment,
            payment == "card" ? 1 : null,
            null
        );

    private async Task<(Guid OrderId, long RowVersion)> CreateCustomerOrderAsync(
        string emailPrefix,
        Guid productId,
        int qty = 1)
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"{emailPrefix}{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);
        var response = await TestHelpers.PostOrderAsync(_client, OrderRequest(productId, qty));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        return (order!.Id, order.RowVersion);
    }

    private async Task SetAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    [Fact]
    public async Task Admin_CanAccessDashboard()
    {
        await SetAdminAsync();
        var response = await _client.GetAsync("/api/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<AdminDashboardDto>(JsonOptions);
        dashboard.Should().NotBeNull();
        dashboard!.TotalOrders.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Customer_GetsForbiddenOnDashboard()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"custdash{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await _client.GetAsync("/api/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_GetsUnauthorizedOnDashboard()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_ExcludesCancelledFromSales()
    {
        var (orderId, version) = await CreateCustomerOrderAsync("cancsale", ProductWaitePocketId);
        await SetAdminAsync();

        var before = await _client.GetFromJsonAsync<AdminDashboardDto>(
            "/api/admin/dashboard", JsonOptions);
        var salesBefore = before!.TotalSales;

        await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "cancelled", note = "teste", expectedVersion = version });

        var after = await _client.GetFromJsonAsync<AdminDashboardDto>(
            "/api/admin/dashboard", JsonOptions);
        after!.Cancelled.Should().BeGreaterThanOrEqualTo(1);
        after.TotalSales.Should().BeLessThan(salesBefore);
    }

    [Fact]
    public async Task AdminList_ReturnsAllCustomersOrders_SortedDesc()
    {
        await CreateCustomerOrderAsync("listA", ProductWaitePocketId);
        await CreateCustomerOrderAsync("listB", ProductCrowleyId);

        await SetAdminAsync();
        var response = await _client.GetAsync("/api/admin/orders?page=1&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrderSummaryDto>>(JsonOptions);
        page!.Items.Should().NotBeEmpty();
        page.Items.Should().BeInDescendingOrder(o => o.CreatedAt);
    }

    [Fact]
    public async Task AdminList_SearchByOrderNumber()
    {
        var (orderId, _) = await CreateCustomerOrderAsync("searchn", ProductWaitePocketId);
        await SetAdminAsync();

        var detail = await _client.GetFromJsonAsync<AdminOrderDetailDto>(
            $"/api/admin/orders/{orderId}", JsonOptions);

        var response = await _client.GetAsync(
            $"/api/admin/orders?search={Uri.EscapeDataString(detail!.OrderNumber)}");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrderSummaryDto>>(JsonOptions);
        page!.Items.Should().Contain(o => o.Id == orderId);
    }

    [Fact]
    public async Task AdminList_FilterByStatus()
    {
        await CreateCustomerOrderAsync("filtstat", ProductWaitePocketId);
        await SetAdminAsync();

        var response = await _client.GetAsync("/api/admin/orders?status=payment_approved");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<AdminOrderSummaryDto>>(JsonOptions);
        page!.Items.Should().OnlyContain(o => o.Status == "payment_approved");
    }

    [Fact]
    public async Task Admin_CanGetOrderDetail_WithFrozenItems()
    {
        var (orderId, _) = await CreateCustomerOrderAsync("detail", ProductWaitePocketId);
        await SetAdminAsync();

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<AdminOrderDetailDto>(JsonOptions);
        detail!.Items.Should().NotBeEmpty();
        detail.Items[0].ProductName.Should().NotBeNullOrWhiteSpace();
        detail.Customer.Email.Should().NotBeNullOrWhiteSpace();
        // Sem CPF no DTO admin
        detail.GetType().GetProperty("Cpf").Should().BeNull();
        typeof(AdminOrderCustomerDto).GetProperty("Cpf").Should().BeNull();
    }

    [Fact]
    public async Task Customer_CannotAccessAdminOrderEndpoints()
    {
        var (orderId, _) = await CreateCustomerOrderAsync("noadmin", ProductWaitePocketId);
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"noadm2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        (await _client.GetAsync("/api/admin/orders")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/admin/orders/{orderId}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "preparing" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SoldProducts_AggregatesQuantities()
    {
        await CreateCustomerOrderAsync("sold1", ProductCrowleyId, qty: 2);
        await CreateCustomerOrderAsync("sold2", ProductCrowleyId, qty: 1);

        await SetAdminAsync();
        var products = await _client.GetFromJsonAsync<AdminSoldProductDto[]>(
            "/api/admin/sales/products", JsonOptions);

        var crowley = products!.FirstOrDefault(p =>
            p.ProductId == ProductCrowleyId ||
            p.ProductName.Contains("Crowley", StringComparison.OrdinalIgnoreCase));
        crowley.Should().NotBeNull();
        crowley!.QuantitySold.Should().BeGreaterThanOrEqualTo(3);
        crowley.OrderCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SoldProducts_UseFrozenName_AfterProductRename()
    {
        var (orderId, _) = await CreateCustomerOrderAsync("frozen", ProductWaitePocketId);
        await SetAdminAsync();

        var detail = await _client.GetFromJsonAsync<AdminOrderDetailDto>(
            $"/api/admin/orders/{orderId}", JsonOptions);
        var frozenName = detail!.Items[0].ProductName;

        await _client.PutAsJsonAsync(
            $"/api/admin/products/{ProductWaitePocketId}",
            new
            {
                name = $"Renomeado {Guid.NewGuid():N}",
                slug = $"renomeado-{Guid.NewGuid():N}",
                shortDescription = "x",
                description = "y",
                price = 59.90m,
                categoryId = Guid.Parse("00000000-0000-0000-0001-000000000001"),
                features = Array.Empty<string>(),
                packageContents = Array.Empty<string>(),
                isFeatured = false,
                isAvailable = true
            });

        var again = await _client.GetFromJsonAsync<AdminOrderDetailDto>(
            $"/api/admin/orders/{orderId}", JsonOptions);
        again!.Items[0].ProductName.Should().Be(frozenName);
    }

    [Fact]
    public async Task Admin_UpdatesValidStatus()
    {
        var (orderId, version) = await CreateCustomerOrderAsync("updstat", ProductWaitePocketId);
        await SetAdminAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "preparing", note = "Em preparação", expectedVersion = version });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order!.Status.Should().Be("preparing");
        order.StatusHistory.Should().HaveCountGreaterThan(1);
        order.RowVersion.Should().Be(version + 1);
    }

    [Fact]
    public async Task Customer_CannotUpdateStatus()
    {
        var (orderId, version) = await CreateCustomerOrderAsync("custupd", ProductWaitePocketId);
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"custupd2{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "shipped", expectedVersion = version });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvalidStatus_IsRejected()
    {
        var (orderId, version) = await CreateCustomerOrderAsync("badstat", ProductWaitePocketId);
        await SetAdminAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "teletransporte", expectedVersion = version });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMissingOrder_ReturnsNotFound()
    {
        await SetAdminAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/status",
            new { status = "preparing", expectedVersion = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentStatusUpdate_ReturnsConflict()
    {
        var (orderId, version) = await CreateCustomerOrderAsync("concurr", ProductWaitePocketId);
        await SetAdminAsync();

        var first = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "preparing", expectedVersion = version });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PatchAsJsonAsync(
            $"/api/admin/orders/{orderId}/status",
            new { status = "shipped", expectedVersion = version });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_CanChangeProductAvailability()
    {
        await SetAdminAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductCrowleyId}/availability",
            new { isAvailable = false });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        var restore = await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductCrowleyId}/availability",
            new { isAvailable = true });
        restore.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Customer_CannotChangeAvailability()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"noavail{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await _client.PatchAsJsonAsync(
            $"/api/admin/products/{ProductCrowleyId}/availability",
            new { isAvailable = false });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomersEndpoint_ReturnsMinimalData_NoCardFields()
    {
        await CreateCustomerOrderAsync("custlist", ProductWaitePocketId);
        await SetAdminAsync();

        var customers = await _client.GetFromJsonAsync<AdminCustomerDto[]>(
            "/api/admin/customers", JsonOptions);
        customers.Should().NotBeEmpty();
        var props = typeof(AdminCustomerDto).GetProperties().Select(p => p.Name).ToHashSet();
        props.Should().NotContain("Cpf");
        props.Should().NotContain("Password");
        props.Should().NotContain("PasswordHash");
        props.Should().Contain("Name");
        props.Should().Contain("Email");
        props.Should().Contain("OrderCount");
        props.Should().Contain("TotalSpent");
    }

    [Fact]
    public void AdminDtos_HaveNoCardSensitiveFields()
    {
        var types = new[]
        {
            typeof(AdminOrderDetailDto),
            typeof(AdminOrderPaymentDto),
            typeof(UpdateOrderStatusRequest)
        };
        foreach (var type in types)
        {
            var names = type.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            names.Should().NotContain("CardNumber");
            names.Should().NotContain("Cvv");
            names.Should().NotContain("CardHolder");
        }
    }
}
