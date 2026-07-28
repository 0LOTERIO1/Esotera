using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Auth;
using FluentAssertions;

namespace Esotera.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsToken()
    {
        var request = new RegisterRequest(
            "Novo Usuário",
            $"novo{Guid.NewGuid():N}@test.com",
            "senha123",
            "11122233344",
            "11999887766",
            AcceptedTerms: true,
            AcceptedPrivacy: true
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be(request.Email.ToLower());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var request = new RegisterRequest(
            "Duplicate User",
            "cliente@esotera.demo",
            "senha123",
            null,
            null,
            AcceptedTerms: true,
            AcceptedPrivacy: true
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var request = new LoginRequest("cliente@esotera.demo", "demo123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.User.Email.Should().Be("cliente@esotera.demo");
    }

    [Fact]
    public async Task Login_Admin_ReturnsTokenWithAdminRole()
    {
        var request = new LoginRequest("admin@esotera.demo", "demo123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrEmpty();
        auth.User.Role.Should().Be("Admin");

        var anonymous = await _client.GetAsync("/api/admin/newsletter");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        TestHelpers.SetBearerToken(_client, auth.Token);
        try
        {
            var ok = await _client.GetAsync("/api/admin/newsletter");
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }
    }
}
