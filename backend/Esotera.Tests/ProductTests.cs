using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Products;
using FluentAssertions;

namespace Esotera.Tests;

public class ProductTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ProductTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CustomerCannotCreateProduct()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateProductRequest(
            "Novo Produto",
            "novo-produto",
            "Descrição curta",
            "Descrição longa",
            99.90m,
            Guid.Parse("00000000-0000-0000-0001-000000000001"),
            null,
            null,
            null
        );

        var response = await _client.PostAsJsonAsync("/api/admin/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCanCreateProduct()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateProductRequest(
            "Produto Admin",
            $"produto-admin-{Guid.NewGuid():N}",
            "Descrição curta",
            "Descrição longa",
            149.90m,
            Guid.Parse("00000000-0000-0000-0001-000000000001"),
            new[] { "Feature 1", "Feature 2" },
            new[] { "Item 1" },
            null,
            true,
            true
        );

        var response = await _client.PostAsJsonAsync("/api/admin/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>(JsonOptions);
        product.Should().NotBeNull();
        product!.Name.Should().Be(request.Name);
        product.Price.Should().Be(request.Price);
    }

    [Fact]
    public async Task InvalidImageUpload_ReturnsError()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);

        var productId = Guid.Parse("11111111-1111-1111-1111-111111111101");

        using var content = new MultipartFormDataContent();
        var textContent = new ByteArrayContent("This is not an image"u8.ToArray());
        textContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(textContent, "file", "test.txt");

        var response = await _client.PostAsync($"/api/admin/products/{productId}/images", content);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }
}
