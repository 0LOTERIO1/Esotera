using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Admin;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Products;
using Esotera.Application.Options;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class ProductAdminTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Guid CategoryTarosId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid ProductWaiteId = Guid.Parse("11111111-1111-1111-1111-111111111101");

    // Minimal valid PNG 1x1
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
        0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4, 0x00, 0x00, 0x00,
        0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    // Minimal JPEG (FF D8 FF ... FF D9)
    private static readonly byte[] TinyJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
        0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
        0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
        0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
        0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
        0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
        0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
        0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x03, 0xFF, 0xC4, 0x00, 0x14, 0x10, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
        0x7F, 0xFF, 0xD9
    ];

    // Minimal WebP (RIFF....WEBP)
    private static readonly byte[] TinyWebp =
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50,
        0x56, 0x50, 0x38, 0x20, 0x18, 0x00, 0x00, 0x00, 0x30, 0x01, 0x00, 0x9D,
        0x01, 0x2A, 0x01, 0x00, 0x01, 0x00, 0x02, 0x00, 0x34, 0x25, 0xA4, 0x00,
        0x03, 0x70, 0x00, 0xFE, 0xFB, 0xFD, 0x50, 0x00
    ];

    public ProductAdminTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AsAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private async Task AsCustomerAsync()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private void ClearAuth() => _client.DefaultRequestHeaders.Authorization = null;

    private async Task<ProductDto> CreateProductAsync(string? slug = null)
    {
        await AsAdminAsync();
        var request = new CreateProductRequest(
            "Produto Teste 2E",
            slug ?? $"produto-2e-{Guid.NewGuid():N}",
            "Curta",
            "Completa",
            88.50m,
            CategoryTarosId,
            new[] { "F1" },
            new[] { "P1" },
            null,
            false,
            true);
        var response = await _client.PostAsJsonAsync("/api/admin/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;
    }

    private MultipartFormDataContent ImageContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Admin_ListsProducts()
    {
        await AsAdminAsync();
        var response = await _client.GetAsync("/api/admin/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<ProductListDto[]>(JsonOptions);
        products.Should().NotBeNull();
        products!.Length.Should().BeGreaterThan(0);
        products.Should().OnlyContain(p => !p.IsArchived);
    }

    [Fact]
    public async Task Customer_Gets403_OnAdminList()
    {
        await AsCustomerAsync();
        var response = await _client.GetAsync("/api/admin/products");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Gets401_OnAdminList()
    {
        ClearAuth();
        var response = await _client.GetAsync("/api/admin/products");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_GetsProductDetails()
    {
        await AsAdminAsync();
        var response = await _client.GetAsync($"/api/admin/products/{ProductWaiteId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        product!.Id.Should().Be(ProductWaiteId);
        product.Images.Should().NotBeEmpty();
        product.RowVersion.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Admin_CreatesValidProduct()
    {
        var product = await CreateProductAsync();
        product.Name.Should().Be("Produto Teste 2E");
        product.Price.Should().Be(88.50m);
        product.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Customer_CannotCreateProduct()
    {
        await AsCustomerAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest(
            "X", "x-slug", null, null, 10m, CategoryTarosId, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DuplicateSlug_IsRejected()
    {
        var first = await CreateProductAsync();
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest(
            "Outro", first.Slug, null, null, 10m, CategoryTarosId, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvalidPrice_IsRejected()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest(
            "Inválido", $"bad-price-{Guid.NewGuid():N}", null, null, 0m, CategoryTarosId, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequiredFields_AreValidated()
    {
        await AsAdminAsync();
        var response = await _client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest(
            "", "", null, null, 10m, Guid.Empty, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_EditsProduct()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        var update = new UpdateProductRequest(
            "Nome Editado", null, null, null, 99.90m, null, null, null, null, null, null, null, product.RowVersion);
        var response = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        updated!.Name.Should().Be("Nome Editado");
        updated.Price.Should().Be(99.90m);
        updated.RowVersion.Should().Be(product.RowVersion + 1);
    }

    [Fact]
    public async Task Customer_CannotEditProduct()
    {
        var product = await CreateProductAsync();
        await AsCustomerAsync();
        var response = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}",
            new UpdateProductRequest("Hack", null, null, null, null, null, null, null, null, null, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ChangesAvailability_CustomerCannot()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        var ok = await _client.PatchAsJsonAsync($"/api/admin/products/{product.Id}/availability",
            new { isAvailable = false });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AsCustomerAsync();
        var forbidden = await _client.PatchAsJsonAsync($"/api/admin/products/{product.Id}/availability",
            new { isAvailable = true });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Archive_HidesFromCatalog_AndBlocksPurchase_RestoreKeepsUnavailable()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        var archive = await _client.PatchAsync($"/api/admin/products/{product.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        var archived = await archive.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        archived!.IsArchived.Should().BeTrue();
        archived.IsAvailable.Should().BeFalse();

        ClearAuth();
        var catalog = await _client.GetFromJsonAsync<ProductListDto[]>("/api/products", JsonOptions);
        catalog!.Should().NotContain(p => p.Id == product.Id);

        var bySlug = await _client.GetAsync($"/api/products/{product.Slug}");
        bySlug.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Purchase blocked
        await AsCustomerAsync();
        var orderResponse = await TestHelpers.PostOrderAsync(_client, new CreateOrderRequest(
            [new CreateOrderItemRequest(product.Id, 1, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null));
        orderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await AsAdminAsync();
        var restore = await _client.PatchAsync($"/api/admin/products/{product.Id}/restore", null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await restore.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        restored!.IsArchived.Should().BeFalse();
        restored.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_Uploads_Jpg_Png_Webp_CustomerCannot()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();

        foreach (var (bytes, name, type) in new[]
                 {
                     (TinyJpeg, "a.jpg", "image/jpeg"),
                     (TinyPng, "b.png", "image/png"),
                     (TinyWebp, "c.webp", "image/webp")
                 })
        {
            using var content = ImageContent(bytes, name, type);
            var response = await _client.PostAsync($"/api/admin/products/{product.Id}/images?isPrimary=false", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var image = await response.Content.ReadFromJsonAsync<ProductImageDto>(JsonOptions);
            image!.SecureUrl.Should().StartWith("https://");
            image.PublicId.Should().NotBeNullOrWhiteSpace();
            // DTO não deve expor segredos
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContain("ApiSecret");
            json.Should().NotContain("CLOUDINARY");
        }

        await AsCustomerAsync();
        using var denied = ImageContent(TinyPng, "x.png", "image/png");
        var forbidden = await _client.PostAsync($"/api/admin/products/{product.Id}/images", denied);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EmptyAndInvalidAndOversizedFiles_AreRejected()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();

        using (var empty = ImageContent([], "empty.png", "image/png"))
        {
            var r = await _client.PostAsync($"/api/admin/products/{product.Id}/images", empty);
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using (var invalid = ImageContent(Encoding.UTF8.GetBytes("not-an-image"), "x.txt", "text/plain"))
        {
            var r = await _client.PostAsync($"/api/admin/products/{product.Id}/images", invalid);
            r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        var huge = new byte[ProductImageLimits.MaxFileSizeBytes + 1];
        TinyPng.CopyTo(huge, 0);
        using (var oversized = ImageContent(huge, "big.png", "image/png"))
        {
            var r = await _client.PostAsync($"/api/admin/products/{product.Id}/images", oversized);
            r.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
        }
    }

    [Fact]
    public async Task ImageLimit_IsRespected()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        for (var i = 0; i < ProductImageLimits.MaxImagesPerProduct; i++)
        {
            using var content = ImageContent(TinyPng, $"i{i}.png", "image/png");
            var response = await _client.PostAsync($"/api/admin/products/{product.Id}/images", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var extra = ImageContent(TinyPng, "extra.png", "image/png");
        var denied = await _client.PostAsync($"/api/admin/products/{product.Id}/images", extra);
        denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Primary_Reorder_AltText_Delete_AndCrossProductProtection()
    {
        var product = await CreateProductAsync();
        var other = await CreateProductAsync();
        await AsAdminAsync();

        ProductImageDto Upload()
        {
            using var content = ImageContent(TinyPng, "g.png", "image/png");
            var response = _client.PostAsync($"/api/admin/products/{product.Id}/images", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadFromJsonAsync<ProductImageDto>(JsonOptions).GetAwaiter().GetResult()!;
        }

        var a = Upload();
        var b = Upload();

        var setPrimary = await _client.PatchAsJsonAsync(
            $"/api/admin/products/{product.Id}/images/{b.Id}",
            new { isPrimary = true, altText = "Alt B" });
        setPrimary.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await _client.GetFromJsonAsync<ProductDto>($"/api/admin/products/{product.Id}", JsonOptions);
        detail!.Images.Count(i => i.IsPrimary).Should().Be(1);
        detail.Images.Single(i => i.IsPrimary).Id.Should().Be(b.Id);
        detail.Images.Single(i => i.Id == b.Id).AltText.Should().Be("Alt B");

        var reorder = await _client.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}/images/order",
            new { imageIds = new[] { a.Id, b.Id } });
        reorder.StatusCode.Should().Be(HttpStatusCode.OK);
        var ordered = await reorder.Content.ReadFromJsonAsync<ProductImageDto[]>(JsonOptions);
        ordered![0].Id.Should().Be(a.Id);
        ordered[0].IsPrimary.Should().BeTrue();

        var crossDelete = await _client.DeleteAsync($"/api/admin/products/{product.Id}/images/{other.Images.FirstOrDefault()?.Id ?? Guid.NewGuid()}");
        // other may have no images — create one on other then try delete via product
        using (var oc = ImageContent(TinyPng, "o.png", "image/png"))
        {
            var or = await _client.PostAsync($"/api/admin/products/{other.Id}/images", oc);
            var oi = await or.Content.ReadFromJsonAsync<ProductImageDto>(JsonOptions);
            var bad = await _client.DeleteAsync($"/api/admin/products/{product.Id}/images/{oi!.Id}");
            bad.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var del = await _client.DeleteAsync($"/api/admin/products/{product.Id}/images/{a.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CloudinaryFailure_DoesNotSimulateSuccess_AndDbFailureCompensates()
    {
        var product = await CreateProductAsync();
        var fake = _factory.Services.GetRequiredService<FakeProductImageStorage>();
        fake.ThrowOnUpload = true;
        await AsAdminAsync();
        using (var content = ImageContent(TinyPng, "f.png", "image/png"))
        {
            var response = await _client.PostAsync($"/api/admin/products/{product.Id}/images", content);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        fake.ThrowOnUpload = false;

        // Compensação: após upload, remove produto → SaveChanges falha → Delete chamado
        fake.AfterUploadHook = async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var entity = await db.Products.FindAsync(product.Id);
            if (entity != null)
            {
                db.Products.Remove(entity);
                await db.SaveChangesAsync();
            }
        };

        var deletedBefore = fake.DeletedPublicIds.Count;
        using (var content = ImageContent(TinyPng, "c.png", "image/png"))
        {
            var response = await _client.PostAsync($"/api/admin/products/{product.Id}/images", content);
            response.IsSuccessStatusCode.Should().BeFalse();
        }
        fake.AfterUploadHook = null;
        fake.DeletedPublicIds.Count.Should().BeGreaterThan(deletedBefore);
    }

    [Fact]
    public async Task Concurrency_DoesNotOverwriteSilently()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        var stale = new UpdateProductRequest(
            "Stale", null, null, null, null, null, null, null, null, null, null, null, product.RowVersion);
        var first = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}",
            new UpdateProductRequest("First", null, null, null, null, null, null, null, null, null, null, null, product.RowVersion));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}", stale);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Catalog_UsesPrimaryImage_AndEditingDoesNotChangeOrderSnapshot()
    {
        var product = await CreateProductAsync();
        await AsAdminAsync();
        using (var content = ImageContent(TinyPng, "p.png", "image/png"))
        {
            var up = await _client.PostAsync($"/api/admin/products/{product.Id}/images?isPrimary=true", content);
            up.EnsureSuccessStatusCode();
        }

        ClearAuth();
        var catalog = await _client.GetFromJsonAsync<ProductListDto[]>("/api/products", JsonOptions);
        var listed = catalog!.Single(p => p.Id == product.Id);
        listed.PrimaryImage.Should().NotBeNullOrWhiteSpace();
        listed.PrimaryImage.Should().StartWith("https://");

        // Create order as customer with inline address
        await AsCustomerAsync();
        var orderResp = await TestHelpers.PostOrderAsync(_client, new CreateOrderRequest(
            [new CreateOrderItemRequest(product.Id, 2, null)],
            new OrderAddressInput("01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"),
            null,
            "melhor_economico",
            "pix",
            null,
            null));
        orderResp.EnsureSuccessStatusCode();
        var order = await orderResp.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        var frozenName = order!.Items[0].ProductName;
        var frozenPrice = order.Items[0].UnitPrice;
        var frozenImage = order.Items[0].ImageUrl;

        await AsAdminAsync();
        var detail = await _client.GetFromJsonAsync<ProductDto>($"/api/admin/products/{product.Id}", JsonOptions);
        await _client.PutAsJsonAsync($"/api/admin/products/{product.Id}",
            new UpdateProductRequest("Nome Novo Histórico", null, null, null, 1.11m, null, null, null, null, null, null, null, detail!.RowVersion));

        await AsCustomerAsync();
        var orderAgain = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}", JsonOptions);
        orderAgain!.Items[0].ProductName.Should().Be(frozenName);
        orderAgain.Items[0].UnitPrice.Should().Be(frozenPrice);
        orderAgain.Items[0].ImageUrl.Should().Be(frozenImage);

        // Archive must not remove historical sales grouping
        await AsAdminAsync();
        await _client.PatchAsync($"/api/admin/products/{product.Id}/archive", null);
        var sold = await _client.GetFromJsonAsync<AdminSoldProductDto[]>("/api/admin/sales/products", JsonOptions);
        sold!.Any(x => x.ProductName == frozenName).Should().BeTrue();
    }

    [Fact]
    public async Task AdminDtos_DoNotExposeSecretsOrPaymentData()
    {
        await AsAdminAsync();
        var response = await _client.GetAsync($"/api/admin/products/{ProductWaiteId}");
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("password");
        json.Should().NotContain("PasswordHash");
        json.Should().NotContain("ApiSecret");
        json.Should().NotContain("cardNumber");
        json.Should().NotContain("CLOUDINARY_API_SECRET");
    }
}
