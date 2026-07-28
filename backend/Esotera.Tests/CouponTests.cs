using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Coupons;
using FluentAssertions;

namespace Esotera.Tests;

public class CouponTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CouponTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidCoupon_ReturnsDiscount()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);

        var request = new CouponValidationRequest("DESCONTO5", 100.00m);

        var response = await _client.PostAsJsonAsync("/api/coupons/validate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.DiscountAmount.Should().Be(5.00m);
    }

    [Fact]
    public async Task CouponBelowMinimum_ReturnsInvalid()
    {
        var token = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);

        var request = new CouponValidationRequest("DESCONTO5", 29.99m);

        var response = await _client.PostAsJsonAsync("/api/coupons/validate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CouponValidationResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mínima");
    }
}
