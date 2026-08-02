using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Integrations;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class MelhorEnvioOAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CustomWebApplicationFactory _factory;

    public MelhorEnvioOAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authorize_AsAdmin_ReturnsAuthorizationUrl_WithShippingCalculateScope()
    {
        var client = _factory.CreateClient();
        var admin = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, admin);

        var response = await client.GetAsync("/api/integrations/melhor-envio/authorize");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MelhorEnvioAuthorizeResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.AuthorizationUrl.Should().StartWith("https://sandbox.melhorenvio.com.br/oauth/authorize");
        body.AuthorizationUrl.Should().Contain("scope=shipping-calculate");
        body.AuthorizationUrl.Should().Contain("response_type=code");
        body.AuthorizationUrl.Should().NotContain("test-me-client-secret");
        body.AuthorizationUrl.Should().NotContain("Bearer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var states = await db.MelhorEnvioOAuthStates.ToListAsync();
        states.Should().NotBeEmpty();
        states.Should().OnlyContain(s => s.StateHash.Length == 64);
        // State em claro nunca persistido.
        var uri = new Uri(body.AuthorizationUrl);
        var plainState = QueryHelpers.ParseQuery(uri.Query)["state"].ToString();
        plainState.Should().NotBeNullOrEmpty();
        states.Should().NotContain(s => s.StateHash == plainState);
    }

    [Fact]
    public async Task Authorize_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/integrations/melhor-envio/authorize");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authorize_WhenConfigMissing_Returns400()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MELHOR_ENVIO_ENABLED"] = "false"
                });
            });
        });

        var client = factory.CreateClient();
        var admin = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, admin);

        var response = await client.GetAsync("/api/integrations/melhor-envio/authorize");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("test-me-client-secret");
        text.Should().NotContain("fake-access");
    }

    [Fact]
    public async Task Callback_WithValidState_ExchangesCode_PersistsCipher_RedirectsConnected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var (plainState, _) = await StartAuthorizeAsync(client);
        var fake = GetFake(_factory);

        var response = await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=test-auth-code-xyz&state={Uri.EscapeDataString(plainState)}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Be("https://esotera.vercel.app/admin/configuracoes?me=connected");
        location.Should().NotContain("test-auth-code");
        location.Should().NotContain("fake-access");
        location.Should().NotContain("access_token");

        fake.ExchangedCodes.Should().Contain("test-auth-code-xyz");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var enc = scope.ServiceProvider.GetRequiredService<IIntegrationsEncryptionService>();
        var conn = await db.MelhorEnvioConnections.SingleAsync();

        conn.AccessTokenCipher.Should().NotBeNullOrWhiteSpace();
        conn.RefreshTokenCipher.Should().NotBeNullOrWhiteSpace();
        conn.AccessTokenCipher.Should().NotContain(fake.LastAccessToken);
        conn.RefreshTokenCipher.Should().NotContain(fake.LastRefreshToken);
        conn.AccessTokenCipher.Should().NotContain("fake-access");
        conn.Scopes.Should().Be("shipping-calculate");
        conn.Environment.Should().Be("sandbox");

        enc.Decrypt(conn.AccessTokenCipher).Should().Be(fake.LastAccessToken);
        enc.Decrypt(conn.RefreshTokenCipher).Should().Be(fake.LastRefreshToken);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(fake.LastAccessToken);
        body.Should().NotContain(fake.LastRefreshToken);
        body.Should().NotContain("test-me-client-secret");
    }

    [Fact]
    public async Task Callback_WithInvalidState_RedirectsStateInvalid()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(
            "/api/integrations/melhor-envio/callback?code=abc&state=estado-invalido");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Contain("me=error");
        location.Should().Contain("reason=state_invalid");
        location.Should().NotContain("abc");
    }

    [Fact]
    public async Task Callback_WhenDenied_RedirectsDenied()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(
            "/api/integrations/melhor-envio/callback?error=access_denied&state=x");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Contain("reason=denied");
        location.Should().NotContain("access_denied");
    }

    [Fact]
    public async Task Callback_WhenStateAlreadyUsed_RedirectsAlreadyUsed()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var (plainState, _) = await StartAuthorizeAsync(client);

        var first = await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=code-1&state={Uri.EscapeDataString(plainState)}");
        first.Headers.Location?.ToString().Should().Contain("me=connected");

        var second = await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=code-2&state={Uri.EscapeDataString(plainState)}");
        second.Headers.Location?.ToString().Should().Contain("reason=already_used");
    }

    [Fact]
    public async Task Callback_WhenExchangeFails_RedirectsExchangeFailed()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var (plainState, _) = await StartAuthorizeAsync(client);
        var fake = GetFake(_factory);
        fake.FailNextExchange = true;

        var response = await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=bad-code&state={Uri.EscapeDataString(plainState)}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Contain("reason=exchange_failed");
        location.Should().NotContain("bad-code");
    }

    [Fact]
    public async Task Status_WhenDisconnected_ReturnsConnectedFalse_WithoutTokens()
    {
        using var factory = CreateIsolatedFactory();
        await ShippingTestHelpers.ClearOAuthConnectionsAsync(factory.Services);
        var client = factory.CreateClient();
        var admin = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, admin);

        var response = await client.GetAsync("/api/admin/integrations/melhor-envio/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<MelhorEnvioStatusDto>(JsonOptions);
        status.Should().NotBeNull();
        status!.Connected.Should().BeFalse();
        status.Configured.Should().BeTrue();

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("access_token");
        raw.Should().NotContain("refresh_token");
        raw.Should().NotContain("fake-access");
        raw.Should().NotContain("test-me-client-secret");
    }

    [Fact]
    public async Task Status_AfterConnect_AndNearExpiry_RefreshesTokens()
    {
        using var factory = CreateIsolatedFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var (plainState, _) = await StartAuthorizeAsync(client, factory);
        var fake = GetFake(factory);

        await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=refresh-setup&state={Uri.EscapeDataString(plainState)}");

        var accessBefore = fake.LastAccessToken;
        var refreshBefore = fake.LastRefreshToken;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var conn = await db.MelhorEnvioConnections.SingleAsync();
            // Dentro da margem de 72h → dispara refresh lazy no status.
            conn.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            await db.SaveChangesAsync();
        }

        var admin = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, admin);
        var response = await client.GetAsync("/api/admin/integrations/melhor-envio/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        fake.RefreshedTokens.Should().Contain(refreshBefore);
        fake.LastAccessToken.Should().NotBe(accessBefore);
        fake.LastRefreshToken.Should().NotBe(refreshBefore);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
            var enc = scope.ServiceProvider.GetRequiredService<IIntegrationsEncryptionService>();
            var conn = await db.MelhorEnvioConnections.SingleAsync();
            enc.Decrypt(conn.AccessTokenCipher).Should().Be(fake.LastAccessToken);
            enc.Decrypt(conn.RefreshTokenCipher).Should().Be(fake.LastRefreshToken);
            conn.AccessTokenCipher.Should().NotContain(fake.LastAccessToken);
        }

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(fake.LastAccessToken);
        raw.Should().NotContain(fake.LastRefreshToken);
        raw.Should().NotContain("test-me-client-secret");

        var status = JsonSerializer.Deserialize<MelhorEnvioStatusDto>(raw, JsonOptions);
        status!.Connected.Should().BeTrue();
        status.AccessTokenValid.Should().BeTrue();
        status.Scopes.Should().Be("shipping-calculate");
    }

    [Fact]
    public async Task GetValidAccessToken_ReturnsDecryptedToken_WithoutExposingInHttp()
    {
        using var factory = CreateIsolatedFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var (plainState, _) = await StartAuthorizeAsync(client, factory);
        await client.GetAsync(
            $"/api/integrations/melhor-envio/callback?code=token-read&state={Uri.EscapeDataString(plainState)}");

        using var scope = factory.Services.CreateScope();
        var oauth = scope.ServiceProvider.GetRequiredService<IMelhorEnvioOAuthService>();
        var fake = GetFake(factory);
        var token = await oauth.GetValidAccessTokenAsync();
        token.Should().Be(fake.LastAccessToken);
    }

    [Fact]
    public void Encryption_RoundTrip_AndRejectsTampering()
    {
        using var scope = _factory.Services.CreateScope();
        var enc = scope.ServiceProvider.GetRequiredService<IIntegrationsEncryptionService>();
        enc.IsConfigured.Should().BeTrue();

        var cipher = enc.Encrypt("segredo-de-teste");
        cipher.Should().NotContain("segredo-de-teste");
        enc.Decrypt(cipher).Should().Be("segredo-de-teste");

        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);
        var act = () => enc.Decrypt(tampered);
        act.Should().Throw<Exception>();
    }

    private static FakeMelhorEnvioOAuthClient GetFake(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<FakeMelhorEnvioOAuthClient>();

    private static CustomWebApplicationFactory CreateIsolatedFactory() => new();

    private static async Task<(string PlainState, string AuthorizationUrl)> StartAuthorizeAsync(
        HttpClient client,
        WebApplicationFactory<Program>? factory = null)
    {
        var admin = await TestHelpers.GetAdminTokenAsync(client);
        TestHelpers.SetBearerToken(client, admin);
        var response = await client.GetAsync("/api/integrations/melhor-envio/authorize");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MelhorEnvioAuthorizeResponse>(JsonOptions);
        var uri = new Uri(body!.AuthorizationUrl);
        var plainState = QueryHelpers.ParseQuery(uri.Query)["state"].ToString();
        if (string.IsNullOrEmpty(plainState))
            throw new InvalidOperationException("state ausente");
        return (plainState, body.AuthorizationUrl);
    }
}
