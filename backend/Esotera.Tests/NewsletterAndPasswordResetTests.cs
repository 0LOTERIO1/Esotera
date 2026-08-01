using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Auth;
using Esotera.Application.DTOs.Newsletter;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

public class NewsletterAndPasswordResetTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public NewsletterAndPasswordResetTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Newsletter_Subscribe_Duplicate_Reactivate_And_Unsubscribe()
    {
        var email = $"news{Guid.NewGuid():N}@test.com";

        var sub = await _client.PostAsJsonAsync("/api/newsletter/subscribe",
            new SubscribeNewsletterRequest(email, true));
        sub.StatusCode.Should().Be(HttpStatusCode.OK);

        var dup = await _client.PostAsJsonAsync("/api/newsletter/subscribe",
            new SubscribeNewsletterRequest(email, true));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Esotera.Infrastructure.Persistence.EsoteraDbContext>();
            var entity = db.NewsletterSubscriptions.First(s => s.Email == email);
            var plain = SecureToken.GenerateUrlSafeToken();
            entity.UnsubscribeTokenHash = SecureToken.Sha256Hex(plain);
            await db.SaveChangesAsync();

            var unsub = await _client.GetAsync($"/api/newsletter/unsubscribe?token={Uri.EscapeDataString(plain)}");
            unsub.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var reactivate = await _client.PostAsJsonAsync("/api/newsletter/subscribe",
            new SubscribeNewsletterRequest(email, true));
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysGeneric_And_ResetWorks()
    {
        var email = $"reset{Guid.NewGuid():N}@test.com";
        await TestHelpers.RegisterNewUserAsync(_client, email);

        var unknown = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("naoexiste@test.com"));
        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<PasswordMessageResponse>(JsonOptions);
        unknownBody!.Message.Should().Be(AuthService.GenericPasswordResetMessage);

        var known = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        known.StatusCode.Should().Be(HttpStatusCode.OK);
        var knownBody = await known.Content.ReadFromJsonAsync<PasswordMessageResponse>(JsonOptions);
        knownBody!.Message.Should().Be(AuthService.GenericPasswordResetMessage);

        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var emailSender = scope.ServiceProvider.GetRequiredService<CapturingEmailSender>();
            emailSender.Sent.Should().NotBeEmpty();
            var html = emailSender.Sent.Last().HtmlBody;
            const string marker = "token=";
            var tokenStart = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            var tokenEnd = html.IndexOfAny(['"', '&', '<'], tokenStart);
            token = Uri.UnescapeDataString(html[tokenStart..tokenEnd]);
        }

        var badConfirm = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(token, "novaSenha1", "outra"));
        badConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(token, "novaSenha1", "novaSenha1"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuse = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(token, "outraSenha2", "outraSenha2"));
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "novaSenha1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Newsletter_Subscribe_SendsConfirmationEmail_WithUnsubscribeLink()
    {
        var email = $"newsmail{Guid.NewGuid():N}@test.com";

        var sub = await _client.PostAsJsonAsync("/api/newsletter/subscribe",
            new SubscribeNewsletterRequest(email, true));
        sub.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<CapturingEmailSender>();
        emailSender.Sent.Should().Contain(m =>
            m.To == email && m.HtmlBody.Contains("/newsletter/descadastrar?token="));

        var html = emailSender.Sent.Last(m => m.To == email).HtmlBody;
        const string marker = "token=";
        var tokenStart = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var tokenEnd = html.IndexOfAny(['"', '&', '<'], tokenStart);
        var token = Uri.UnescapeDataString(html[tokenStart..tokenEnd]);

        var unsub = await _client.GetAsync($"/api/newsletter/unsubscribe?token={Uri.EscapeDataString(token)}");
        unsub.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminNewsletter_RequiresAdmin()
    {
        var customerToken = await TestHelpers.GetCustomerTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, customerToken);

        var denied = await _client.GetAsync("/api/admin/newsletter");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var ok = await _client.GetAsync("/api/admin/newsletter");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
