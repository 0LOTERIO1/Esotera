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
/// HTTP client read-only: searchOrderByCode (schema real). Sem retry. Sem mutations.
/// </summary>
public sealed class J3OrderLookupHttpClient : IJ3OrderLookupClient
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
    private readonly ILogger<J3OrderLookupHttpClient> _logger;

    public J3OrderLookupHttpClient(
        HttpClient http,
        IOptions<J3ShippingOptions> options,
        IJ3SellerAuthProvider sellerAuth,
        ILogger<J3OrderLookupHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _sellerAuth = sellerAuth;
        _logger = logger;
    }

    public async Task<J3OrderLookupResult> SearchByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        var code = orderCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.ConfirmMismatch);

        if (!_options.HasValidGraphQlUrl || !_options.HasSellerBearerSource)
            return J3OrderLookupResult.Failed(J3FulfillmentErrorCodes.Configuration);

        var (bearer, authError) = await J3SellerBearerResolver.ResolveAsync(
            _options, _sellerAuth, cancellationToken);
        if (string.IsNullOrWhiteSpace(bearer))
            return J3OrderLookupResult.Failed(authError ?? J3FulfillmentErrorCodes.AuthLoginFailed);

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
            query = J3SearchOrderByCodeQuery.Document,
            operationName = J3SearchOrderByCodeQuery.OperationName,
            variables = new { code }
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
            _logger.LogWarning("J3 order lookup failed: network/timeout");
            return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "J3 order lookup failed: HTTP {StatusCode}",
                    (int)response.StatusCode);
                return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
            }

            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                _logger.LogWarning("J3 order lookup failed: invalid JSON");
                return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Array
                    && errors.GetArrayLength() > 0)
                {
                    _logger.LogWarning("J3 order lookup failed: GraphQL errors");
                    return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
                }

                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return J3OrderLookupResult.NotFound();
                }

                if (!data.TryGetProperty("searchOrderByCode", out var node)
                    || node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return J3OrderLookupResult.NotFound();
                }

                if (node.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("J3 order lookup failed: unexpected searchOrderByCode shape");
                    return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
                }

                J3SearchOrderByCodeResponseDto? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<J3SearchOrderByCodeResponseDto>(
                        node.GetRawText(),
                        JsonOptions);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("J3 order lookup failed: DTO deserialize");
                    return J3OrderLookupResult.Failed(J3ReconcileErrorCodes.LookupFailed);
                }

                if (parsed is null || string.IsNullOrWhiteSpace(parsed.Id))
                    return J3OrderLookupResult.NotFound();

                // Snapshot final exige Order local (CEP) — montado no admin service.
                // Aqui devolvemos o DTO tipado; Outcome Found com Response preenchido.
                _logger.LogInformation(
                    "J3 order lookup succeeded operation {Operation} j3OrderId present store {StorePresent}",
                    J3SearchOrderByCodeQuery.OperationName,
                    !string.IsNullOrWhiteSpace(parsed.StoreName));

                return new J3OrderLookupResult
                {
                    Outcome = J3OrderLookupOutcome.Found,
                    Response = parsed with
                    {
                        DeliveryPoints = parsed.DeliveryPoints ?? []
                    }
                };
            }
        }
    }
}
