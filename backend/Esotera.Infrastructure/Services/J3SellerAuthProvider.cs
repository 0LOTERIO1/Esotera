using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Login Seller J3 (REST portal) + cache em memória do accessToken.
/// Valida mySellerMetadata.sellerId contra J3_SELLER_ID após login.
/// Singleton — cache compartilhado. Nunca loga password/token/response completa.
/// </summary>
public sealed class J3SellerAuthProvider : IJ3SellerAuthProvider, IDisposable
{
    public const string HttpClientName = "j3-seller-auth";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string VerifySellerQuery =
        """
        query VerifySellerLogin {
          mySellerMetadata {
            sellerId
          }
        }
        """;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly J3ShippingOptions _options;
    private readonly ILogger<J3SellerAuthProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _renewAfterUtc = DateTimeOffset.MinValue;

    public J3SellerAuthProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<J3ShippingOptions> options,
        ILogger<J3SellerAuthProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public void InvalidateCachedToken()
    {
        _cachedToken = null;
        _renewAfterUtc = DateTimeOffset.MinValue;
        _logger.LogInformation("J3 seller auth cache invalidated");
    }

    public async Task<J3SellerAuthResult> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.HasSellerLoginCredentials)
            return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthConfiguration);

        if (!_options.HasValidLoginUrl)
            return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthConfiguration);

        var now = DateTimeOffset.UtcNow;
        var cached = _cachedToken;
        if (!string.IsNullOrWhiteSpace(cached) && now < _renewAfterUtc)
            return J3SellerAuthResult.Success(cached);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            cached = _cachedToken;
            if (!string.IsNullOrWhiteSpace(cached) && now < _renewAfterUtc)
                return J3SellerAuthResult.Success(cached);

            return await LoginAndCacheAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<J3SellerAuthResult> LoginAndCacheAsync(CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.LoginUrl!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Origin", "https://app.j3tms.com.br");
        request.Headers.TryAddWithoutValidation("Referer", "https://app.j3tms.com.br/");

        var payload = new
        {
            credentials = new
            {
                email = _options.LoginEmail!.Trim(),
                password = _options.LoginPassword
            }
        };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            _logger.LogWarning("J3 seller auth login failed: network/timeout");
            return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                _logger.LogWarning("J3 seller auth login failed: cannot read body HTTP {StatusCode}", status);
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
            }

            if (status == 401 || status == 403)
            {
                _logger.LogWarning("J3 seller auth login failed: HTTP {StatusCode}", status);
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthHttp401);
            }

            if (status < 200 || status >= 300)
            {
                _logger.LogWarning("J3 seller auth login failed: HTTP {StatusCode}", status);
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _logger.LogWarning("J3 seller auth login failed: invalid JSON");
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthJsonInvalid);
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("accessToken", out var tokenEl)
                    || tokenEl.ValueKind != JsonValueKind.String)
                {
                    _logger.LogWarning("J3 seller auth login failed: accessToken missing");
                    return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthTokenMissing);
                }

                var accessToken = tokenEl.GetString();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogWarning("J3 seller auth login failed: accessToken empty");
                    return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthTokenMissing);
                }

                var verify = await VerifySellerIdAsync(http, accessToken, cancellationToken);
                if (!verify.IsSuccess)
                    return verify;

                StoreCache(accessToken);
                _logger.LogInformation("J3 seller auth login succeeded");
                return J3SellerAuthResult.Success(accessToken);
            }
        }
    }

    private async Task<J3SellerAuthResult> VerifySellerIdAsync(
        HttpClient http,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var expected = _options.SellerId?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            // Sem SellerId configurado: skip identity check (caller ainda exige sellerId nas mutations).
            return J3SellerAuthResult.Success(accessToken);
        }

        if (!_options.HasValidGraphQlUrl)
            return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthConfiguration);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.GraphQlUrl!.Trim());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var companyGroup = string.IsNullOrWhiteSpace(_options.CompanyGroupCode)
            ? "J3"
            : _options.CompanyGroupCode.Trim();
        request.Headers.TryAddWithoutValidation("x-company-group-code", companyGroup);

        var gql = new
        {
            query = VerifySellerQuery,
            operationName = "VerifySellerLogin",
            variables = new { }
        };
        request.Content = new StringContent(
            JsonSerializer.Serialize(gql, JsonOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            _logger.LogWarning("J3 seller auth verify failed: network/timeout");
            return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "J3 seller auth verify failed: HTTP {StatusCode}",
                    (int)response.StatusCode);
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                _logger.LogWarning("J3 seller auth verify failed: invalid JSON");
                return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthJsonInvalid);
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Array
                    && errors.GetArrayLength() > 0)
                {
                    _logger.LogWarning("J3 seller auth verify failed: GraphQL errors");
                    return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthLoginFailed);
                }

                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || data.ValueKind != JsonValueKind.Object
                    || !data.TryGetProperty("mySellerMetadata", out var meta)
                    || meta.ValueKind != JsonValueKind.Object
                    || !meta.TryGetProperty("sellerId", out var sellerEl)
                    || sellerEl.ValueKind != JsonValueKind.String)
                {
                    _logger.LogWarning("J3 seller auth verify failed: sellerId missing");
                    return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthSellerMismatch);
                }

                var actual = sellerEl.GetString()?.Trim();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("J3 seller auth verify failed: sellerId mismatch");
                    return J3SellerAuthResult.Fail(J3FulfillmentErrorCodes.AuthSellerMismatch);
                }

                return J3SellerAuthResult.Success(accessToken);
            }
        }
    }

    private void StoreCache(string accessToken)
    {
        var skew = _options.AuthRenewSkewMinutes > 0
            ? TimeSpan.FromMinutes(Math.Clamp(_options.AuthRenewSkewMinutes, 1, 30))
            : TimeSpan.FromMinutes(5);

        var exp = J3JwtExpReader.TryReadExpiresAtUtc(accessToken);
        if (exp is { } expiresAt)
        {
            var renew = expiresAt - skew;
            // Já dentro da janela de skew: não estende cache — próximo Get força relogin.
            if (renew <= DateTimeOffset.UtcNow)
                renew = DateTimeOffset.UtcNow;
            _renewAfterUtc = renew;
        }
        else
        {
            // Sem exp legível: TTL conservador 50 min.
            _renewAfterUtc = DateTimeOffset.UtcNow.AddMinutes(50);
        }

        _cachedToken = accessToken;
    }

    public void Dispose() => _gate.Dispose();
}
