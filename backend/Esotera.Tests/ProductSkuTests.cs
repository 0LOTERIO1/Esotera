using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Products;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class ProductSkuTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Guid CategoryTarosId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid ProductWaiteInicianteId = Guid.Parse("11111111-1111-1111-1111-111111111107");
    private static readonly Guid ProductWaitePocketId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    public ProductSkuTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AsAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private static CreateProductRequest CreateNoVariation(
        string name,
        string slug,
        string? sku,
        ProductVariationDto[]? variations = null) =>
        new(
            name,
            slug,
            "Curta",
            "Completa",
            42.50m,
            CategoryTarosId,
            null,
            null,
            variations,
            false,
            true,
            false,
            sku);

    [Fact]
    public async Task Create_WithoutVariation_WithSku_Saves()
    {
        await AsAdminAsync();
        var sku = $"SKU-NEW-{Guid.NewGuid():N}"[..20];
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Produto SKU", $"produto-sku-{Guid.NewGuid():N}", sku));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        product!.Sku.Should().Be(sku);
    }

    [Fact]
    public async Task Create_WithoutVariation_WithoutSku_Rejected()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Sem SKU", $"sem-sku-{Guid.NewGuid():N}", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TrimsSku()
    {
        await AsAdminAsync();
        var raw = $"  SKU-TRIM-{Guid.NewGuid():N}  ";
        var expected = raw.Trim();
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Trim SKU", $"trim-sku-{Guid.NewGuid():N}", raw));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        product!.Sku.Should().Be(expected);
    }

    [Fact]
    public async Task Create_SkuLongerThan64_Rejected()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("SKU longo", $"sku-long-{Guid.NewGuid():N}", new string('X', 65)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateSku_Rejected()
    {
        await AsAdminAsync();
        var sku = $"SKU-DUP-{Guid.NewGuid():N}"[..22];
        var first = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Dup 1", $"dup-1-{Guid.NewGuid():N}", sku));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Dup 2", $"dup-2-{Guid.NewGuid():N}", sku));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Sku_Saves()
    {
        await AsAdminAsync();
        var create = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation("Upd SKU", $"upd-sku-{Guid.NewGuid():N}", $"SKU-A-{Guid.NewGuid():N}"[..18]));
        var product = (await create.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;
        var newSku = $"SKU-B-{Guid.NewGuid():N}"[..18];

        var update = new UpdateProductRequest(
            null, null, null, null, null, null, null, null, null, null, null, null,
            product.RowVersion, newSku);
        var response = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        updated!.Sku.Should().Be(newSku);
    }

    [Fact]
    public async Task LegacyProduct_WithNullSku_IsReadable()
    {
        await AsAdminAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var id = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = id,
            Slug = $"legado-null-sku-{id:N}",
            Name = "Legado sem SKU",
            Sku = null,
            Price = 10m,
            CategoryId = CategoryTarosId,
            IsAvailable = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/admin/products/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        product!.Sku.Should().BeNull();
    }

    [Fact]
    public async Task Update_WithVariations_PreservesVariationSku()
    {
        await AsAdminAsync();
        var get = await _client.GetAsync($"/api/admin/products/{ProductWaiteInicianteId}");
        var detail = (await get.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;
        detail.Variations.Should().NotBeNull();
        var originalSkus = detail.Variations!.Select(v => v.Sku).ToArray();
        originalSkus.Should().Contain("SKU-WAITE-TAROT");

        var update = new UpdateProductRequest(
            detail.Name,
            detail.Slug,
            detail.ShortDescription,
            detail.Description,
            detail.Price,
            detail.CategoryId,
            detail.Features,
            detail.PackageContents,
            detail.Variations,
            detail.IsFeatured,
            detail.IsAvailable,
            detail.IsDemo,
            detail.RowVersion,
            detail.Sku);
        var response = await _client.PutAsJsonAsync($"/api/admin/products/{detail.Id}", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        updated!.Variations!.Select(v => v.Sku).Should().Equal(originalSkus);
    }

    [Fact]
    public async Task CreateOrder_WithoutVariation_SnapshotsProductSku()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"skusnap{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaitePocketId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", true),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == order!.Id);
        item.Sku.Should().Be("SKU-WAITE-POCKET");
    }

    [Fact]
    public async Task CreateOrder_WithVariation_SnapshotsVariationSku_NotProductSku()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"skuvar{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaiteInicianteId, 1, "var-somente-taro")],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", true),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == order!.Id);
        item.Sku.Should().Be("SKU-WAITE-TAROT");
    }

    [Fact]
    public async Task ChangingProductSku_DoesNotRewriteOldOrderItemSku()
    {
        await AsAdminAsync();
        var create = await _client.PostAsJsonAsync(
            "/api/admin/products",
            CreateNoVariation(
                "Snapshot imutável",
                $"sku-immutable-{Guid.NewGuid():N}",
                $"SKU-OLD-{Guid.NewGuid():N}"[..20]));
        var product = (await create.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"skuimm{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var orderResponse = await TestHelpers.PostOrderAsync(
            _client,
            new CreateOrderRequest(
                [new CreateOrderItemRequest(product.Id, 1, null)],
                new OrderAddressInput(
                    "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", true),
                null,
                "melhor_economico",
                "pix",
                null,
                null));
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        var originalSku = product.Sku;

        await AsAdminAsync();
        var newSku = $"SKU-NEW-{Guid.NewGuid():N}"[..20];
        var update = new UpdateProductRequest(
            null, null, null, null, null, null, null, null, null, null, null, null,
            product.RowVersion, newSku);
        var upd = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}", update);
        upd.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.OrderId == order!.Id);
        item.Sku.Should().Be(originalSku);
        var refreshed = await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        refreshed.Sku.Should().Be(newSku);
    }

    [Fact]
    public async Task CreateOrder_WithoutVariation_WithoutSku_Fails()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var id = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = id,
            Slug = $"no-sku-order-{id:N}",
            Name = "Sem SKU pedido",
            Sku = null,
            Price = 15m,
            CategoryId = CategoryTarosId,
            IsAvailable = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"noskuord{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await TestHelpers.PostOrderAsync(
            _client,
            new CreateOrderRequest(
                [new CreateOrderItemRequest(id, 1, null)],
                new OrderAddressInput(
                    "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", true),
                null,
                "melhor_economico",
                "pix",
                null,
                null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
