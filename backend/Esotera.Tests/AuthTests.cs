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

    [Fact]
    public async Task Register_MaskedCpf_IsAccepted()
    {
        // CPF válido com máscara — normalização remove não-dígitos antes da regra.
        var request = new RegisterRequest(
            "Usuário Máscara",
            $"mask{Guid.NewGuid():N}@test.com",
            "senha123",
            "529.982.247-25",
            "(11) 98888-7777",
            AcceptedTerms: true,
            AcceptedPrivacy: true
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.User.Cpf.Should().Be("52998224725");
    }

    [Fact]
    public async Task Register_IncompleteCpf_ReturnsCpfFieldError()
    {
        var request = new RegisterRequest(
            "CPF Curto",
            $"cpf{Guid.NewGuid():N}@test.com",
            "senha123",
            "123.456",
            "11999887766",
            AcceptedTerms: true,
            AcceptedPrivacy: true
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.TryGetProperty("errors", out var errors).Should().BeTrue();

        // Chave do FluentValidation PropertyName; pode ser Cpf ou cpf.
        var hasCpf =
            (errors.TryGetProperty("Cpf", out var cpfPascal) && cpfPascal.GetArrayLength() > 0)
            || (errors.TryGetProperty("cpf", out var cpfCamel) && cpfCamel.GetArrayLength() > 0);
        hasCpf.Should().BeTrue("erro de CPF deve vir na chave Cpf/cpf, não em Email");

        errors.TryGetProperty("Email", out _).Should().BeFalse();
        errors.TryGetProperty("email", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsEmailFieldError()
    {
        var request = new RegisterRequest(
            "Email Ruim",
            "nao-e-email",
            "senha123",
            "52998224725",
            "11999887766",
            AcceptedTerms: true,
            AcceptedPrivacy: true
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.TryGetProperty("errors", out var errors).Should().BeTrue();

        var hasEmail =
            (errors.TryGetProperty("Email", out var emailPascal) && emailPascal.GetArrayLength() > 0)
            || (errors.TryGetProperty("email", out var emailCamel) && emailCamel.GetArrayLength() > 0);
        hasEmail.Should().BeTrue();
    }
}
