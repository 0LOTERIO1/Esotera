using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// HTTP client read-only: getOrderDetails (schema real). Sem retry. Sem mutations.
/// </summary>
public sealed class J3OrderDetailsHttpClient : IJ3OrderDetailsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly J3ShippingOptions _options;
    private readonly IJ3SellerAuthProvider _sellerAuth;
    private readonly ILogger<J3OrderDetailsHttpClient> _logger;

    public J3OrderDetailsHttpClient(
        HttpClient http,
        IOptions<J3ShippingOptions> options,
        IJ3SellerAuthProvider sellerAuth,
        ILogger<J3OrderDetailsHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _sellerAuth = sellerAuth;
        _logger = logger;
    }

    public async Task<J3OrderDetailsLookupResult> GetByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var id = orderId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.NotEligible);

        if (!_options.HasValidGraphQlUrl || !_options.HasSellerBearerSource)
            return J3OrderDetailsLookupResult.Failed(J3FulfillmentErrorCodes.Configuration);

        var (bearer, authError) = await J3SellerBearerResolver.ResolveAsync(
            _options, _sellerAuth, cancellationToken);
        if (string.IsNullOrWhiteSpace(bearer))
            return J3OrderDetailsLookupResult.Failed(
                authError ?? J3FulfillmentErrorCodes.AuthLoginFailed);

        var endpoint = new Uri(_options.GraphQlUrl!.Trim(), UriKind.Absolute);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var companyGroup = string.IsNullOrWhiteSpace(_options.CompanyGroupCode)
            ? "J3"
            : _options.CompanyGroupCode.Trim();
        request.Headers.TryAddWithoutValidation("x-company-group-code", companyGroup);

        var payload = new
        {
            query = J3GetOrderDetailsQuery.Document,
            operationName = J3GetOrderDetailsQuery.OperationName,
            variables = new { orderId = id }
        };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            _logger.LogWarning("J3 order details lookup failed: network/timeout");
            return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "J3 order details lookup failed: HTTP {StatusCode}",
                    (int)response.StatusCode);
                return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
            }

            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _logger.LogWarning("J3 order details lookup failed: invalid JSON");
                return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Array
                    && errors.GetArrayLength() > 0)
                {
                    _logger.LogWarning("J3 order details lookup failed: GraphQL errors");
                    return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
                }

                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return J3OrderDetailsLookupResult.NotFound();
                }

                if (!data.TryGetProperty("getOrderDetails", out var node)
                    || node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return J3OrderDetailsLookupResult.NotFound();
                }

                if (node.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("J3 order details lookup failed: unexpected getOrderDetails shape");
                    return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
                }

                J3OrderDetailsDto? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<J3OrderDetailsDto>(
                        node.GetRawText(),
                        JsonOptions);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("J3 order details lookup failed: DTO deserialize");
                    return J3OrderDetailsLookupResult.Failed(J3IdentifierHydrationErrorCodes.LookupFailed);
                }

                if (parsed is null || string.IsNullOrWhiteSpace(parsed.Id))
                    return J3OrderDetailsLookupResult.NotFound();

                _logger.LogInformation(
                    "J3 order details lookup succeeded operation {Operation} j3OrderId present deliveryPoint {HasDp}",
                    J3GetOrderDetailsQuery.OperationName,
                    parsed.DeliveryPoint is not null);

                return J3OrderDetailsLookupResult.Found(parsed);
            }
        }
    }
}
