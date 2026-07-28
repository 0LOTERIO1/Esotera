using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Auth;
using Esotera.Application.DTOs.Orders;

namespace Esotera.Tests;

public static class TestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<string> GetAdminTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "admin@esotera.demo",
            "demo123"
        ));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.Token;
    }

    public static async Task<string> GetCustomerTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "cliente@esotera.demo",
            "demo123"
        ));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.Token;
    }

    public static async Task<(string Token, Guid UserId)> RegisterNewUserAsync(
        HttpClient client,
        string email = "newuser@test.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Test User",
            email,
            "password123",
            "12345678900",
            "11999999999",
            AcceptedTerms: true,
            AcceptedPrivacy: true
        ));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.Token, auth.User.Id);
    }

    public static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<HttpResponseMessage> PostOrderAsync(
        HttpClient client,
        CreateOrderRequest request,
        string? idempotencyKey = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            idempotencyKey ?? Guid.NewGuid().ToString());

        return await client.SendAsync(message);
    }
}
