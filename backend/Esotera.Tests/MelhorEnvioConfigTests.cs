using Esotera.Application.Options;
using FluentAssertions;
using Xunit;

namespace Esotera.Tests;

/// <summary>
/// Fase 1 da integração real: ambiente deixa de ser gate do OAuth e as URLs
/// passam a derivar da base configurada.
/// </summary>
public class MelhorEnvioConfigTests
{
    private static MelhorEnvioOptions FullyConfigured(string environment, string? baseUrl = null) =>
        new()
        {
            Enabled = true,
            Environment = environment,
            BaseUrl = baseUrl,
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://api.example.com/api/integrations/melhor-envio/callback",
            UserAgent = "Esotera (contato@example.com)",
            FrontendBaseUrl = "https://example.com"
        };

    [Fact]
    public void Sandbox_DerivesSandboxUrls()
    {
        var options = FullyConfigured("sandbox");

        options.IsSandbox.Should().BeTrue();
        options.NormalizedEnvironment.Should().Be("sandbox");
        options.ResolvedBaseUrl.Should().Be(MelhorEnvioOptions.SandboxBaseUrl);
        options.AuthorizeUrl.Should().Be(MelhorEnvioOptions.SandboxAuthorizeUrl);
        options.TokenUrl.Should().Be(MelhorEnvioOptions.SandboxTokenUrl);
        options.CalculateUrl.Should().Be(MelhorEnvioOptions.SandboxCalculateUrl);
    }

    [Fact]
    public void Production_IsOAuthConfigured_AndDerivesProductionUrls()
    {
        var options = FullyConfigured("production");

        options.IsSandbox.Should().BeFalse();
        options.NormalizedEnvironment.Should().Be("production");
        // Regressão: antes o gate exigia IsSandbox e desligava a integração em produção.
        options.IsOAuthConfigured.Should().BeTrue();
        options.ResolvedBaseUrl.Should().Be(MelhorEnvioOptions.ProductionBaseUrl);
        options.CalculateUrl.Should().Be($"{MelhorEnvioOptions.ProductionBaseUrl}/api/v2/me/shipment/calculate");
    }

    [Fact]
    public void UnknownEnvironment_FallsBackToSandbox()
    {
        var options = FullyConfigured("staging");

        options.IsSandbox.Should().BeTrue();
        options.ResolvedBaseUrl.Should().Be(MelhorEnvioOptions.SandboxBaseUrl);
    }

    [Fact]
    public void ExplicitBaseUrl_OverridesEnvironmentDefault_AndTrimsTrailingSlash()
    {
        var options = FullyConfigured("production", "https://custom.melhorenvio.com.br/");

        options.ResolvedBaseUrl.Should().Be("https://custom.melhorenvio.com.br");
        options.TokenUrl.Should().Be("https://custom.melhorenvio.com.br/oauth/token");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://melhorenvio.com.br")]
    public void InvalidBaseUrl_BlocksOAuthConfiguration(string baseUrl)
    {
        var options = FullyConfigured("production", baseUrl);

        options.HasValidBaseUrl.Should().BeFalse();
        options.IsOAuthConfigured.Should().BeFalse();
    }

    [Fact]
    public void Disabled_IsNeverOAuthConfigured()
    {
        var options = FullyConfigured("sandbox");
        options.Enabled = false;

        options.IsOAuthConfigured.Should().BeFalse();
    }

    [Fact]
    public void RequiredScope_DoesNotAllowLabelPurchase()
    {
        // Guarda-vida: comprar etiqueta exigirá escopo novo + reautorização.
        MelhorEnvioOptions.RequiredScope.Should().Be("shipping-calculate");
    }
}
