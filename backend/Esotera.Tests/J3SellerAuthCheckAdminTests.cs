using System.Net;
using System.Text.Json;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// Smoke GET /api/admin/j3/auth-check — zero mutations create/import.
/// </summary>
public class J3SellerAuthCheckAdminTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public J3SellerAuthCheckAdminTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_AuthSuccess_Returns200_WithoutSecrets()
    {
        ResetFakes(success: true);
        await SetAdminAsync();

        var response = await _client.GetAsync("/api/admin/j3/auth-check");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        AssertNoSecrets(body);

        var dto = JsonSerializer.Deserialize<J3SellerAuthCheckResponse>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.Success.Should().BeTrue();
        dto.Authenticated.Should().BeTrue();
        dto.SellerValidated.Should().BeTrue();
        dto.AuthMode.Should().Be("seller_login");
        dto.ErrorCode.Should().BeNull();

        AssertZeroMutations();
    }

    [Fact]
    public async Task AuthProviderFailure_ReturnsSanitized502()
    {
        ResetFakes(success: false);
        await SetAdminAsync();

        var response = await _client.GetAsync("/api/admin/j3/auth-check");
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var body = await response.Content.ReadAsStringAsync();
        AssertNoSecrets(body);

        var dto = JsonSerializer.Deserialize<J3SellerAuthCheckResponse>(body, JsonOptions);
        dto.Should().NotBeNull();
        dto!.Success.Should().BeFalse();
        dto.Authenticated.Should().BeFalse();
        dto.SellerValidated.Should().BeFalse();
        dto.ErrorCode.Should().Be(J3FulfillmentErrorCodes.AuthSellerMismatch);
        body.Should().NotContain("Exception");
        body.Should().NotContain("StackTrace");

        AssertZeroMutations();
    }

    [Fact]
    public async Task Anonymous_Returns401()
    {
        ResetFakes(success: true);
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/admin/j3/auth-check");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        AssertZeroMutations();
    }

    [Fact]
    public async Task Endpoint_DoesNotCall_CreateOrImport()
    {
        ResetFakes(success: true);
        await SetAdminAsync();

        var beforeCreate = GetFulfillment().CreateCallCount;
        var beforeImport = GetImport().CallCount;

        (await _client.GetAsync("/api/admin/j3/auth-check")).StatusCode.Should().Be(HttpStatusCode.OK);

        GetFulfillment().CreateCallCount.Should().Be(beforeCreate);
        GetImport().CallCount.Should().Be(beforeImport);
        GetAuth().GetCallCount.Should().BeGreaterThan(0);
    }

    private async Task SetAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    private void ResetFakes(bool success)
    {
        var auth = GetAuth();
        auth.Reset();
        if (!success)
        {
            auth.NextResultOverride = J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthSellerMismatch);
        }

        GetFulfillment().Reset();
        GetImport().Reset();
    }

    private void AssertZeroMutations()
    {
        GetFulfillment().CreateCallCount.Should().Be(0);
        GetImport().CallCount.Should().Be(0);
    }

    private static void AssertNoSecrets(string body)
    {
        body.Should().NotContain("accessToken");
        body.Should().NotContain("AccessToken");
        body.Should().NotContain("password");
        body.Should().NotContain("Password");
        body.Should().NotContain("Bearer ");
        body.Should().NotContain("fake-seller-access-token");
    }

    private FakeJ3SellerAuthProvider GetAuth() =>
        _factory.Services.GetRequiredService<FakeJ3SellerAuthProvider>();

    private FakeJ3FulfillmentClient GetFulfillment() =>
        _factory.Services.GetRequiredService<FakeJ3FulfillmentClient>();

    private FakeJ3ImportOrderByAccessKeyClient GetImport() =>
        _factory.Services.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>();
}
