using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Common;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Cliente GraphQL J3 Flex (Passo 3) — coverage + tracking somente.
/// URL vem de <see cref="J3ShippingOptions.GraphQlUrl"/> (sem hardcode de host de produção).
/// Seller IDs / preço / SLA / mutations não entram aqui.
/// CEP: normaliza 8 dígitos e envia #####-### no GraphQL.
/// </summary>
public sealed class J3Client : IJ3Client
{
    private const string OpCoverage = "IsValidServiceArea";
    private const string OpTracking = "GetJ3Tracking";

    private const string CoverageQuery =
        """
        query IsValidServiceArea($input: IsValidServiceAreaInput!) {
          isValidServiceArea(input: $input)
        }
        """;

    private const string TrackingQuery =
        """
        query GetJ3Tracking($trackingNumber: String!) {
          getTrackingOrderSeller(trackingNumber: $trackingNumber) {
            id
            code
            status
            ecommerce
            createdAt
            collectedAt
            completedAt
            canceledAt
          }
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly J3ShippingOptions _options;
    private readonly ILogger<J3Client> _logger;

    public J3Client(
        HttpClient http,
        IOptions<J3ShippingOptions> options,
        ILogger<J3Client> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsServiceAreaAsync(
        string zipCode,
        CancellationToken cancellationToken = default)
    {
        // CEP inválido: não dispara HTTP.
        var digits = BrazilianCep.TryNormalize(zipCode);
        if (digits is null)
            throw new J3ApiException(OpCoverage, "J3 coverage: zip code is invalid.");

        EnsureCanCall(OpCoverage);

        var companyGroup = string.IsNullOrWhiteSpace(_options.CompanyGroupCode)
            ? "J3"
            : _options.CompanyGroupCode.Trim();

        // Portal J3 espera #####-### (ex.: 03065-000), não só dígitos.
        var zipMasked = BrazilianCep.FormatMasked(digits);

        var variables = new
        {
            input = new
            {
                zipcode = zipMasked,
                companyGroupCode = companyGroup
            }
        };

        using var doc = await PostGraphQlAsync(OpCoverage, CoverageQuery, variables, cancellationToken);
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || !data.TryGetProperty("isValidServiceArea", out var flag)
            || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            _logger.LogWarning("J3 operation {Operation} failed: malformed coverage payload", OpCoverage);
            throw new J3ApiException(OpCoverage, "J3 coverage: malformed GraphQL data payload.");
        }

        var result = flag.GetBoolean();
        _logger.LogInformation(
            "J3 operation {Operation} succeeded with HTTP {StatusCode}",
            OpCoverage,
            200);
        return result;
    }

    /// <inheritdoc />
    public async Task<J3TrackingResult?> GetTrackingAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        EnsureCanCall(OpTracking);

        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new J3ApiException(OpTracking, "J3 tracking: tracking number is empty.");

        var variables = new { trackingNumber = trackingNumber.Trim() };

        using var doc = await PostGraphQlAsync(OpTracking, TrackingQuery, variables, cancellationToken);
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            _logger.LogWarning("J3 operation {Operation} failed: missing data", OpTracking);
            throw new J3ApiException(OpTracking, "J3 tracking: malformed GraphQL data payload.");
        }

        if (!data.TryGetProperty("getTrackingOrderSeller", out var order)
            || order.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            // Resposta GraphQL válida sem objeto = não encontrado.
            _logger.LogInformation(
                "J3 operation {Operation} succeeded with HTTP {StatusCode} (not found)",
                OpTracking,
                200);
            return null;
        }

        if (order.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("J3 operation {Operation} failed: unexpected tracking shape", OpTracking);
            throw new J3ApiException(OpTracking, "J3 tracking: malformed tracking object.");
        }

        var mapped = new J3TrackingResult
        {
            Id = ReadString(order, "id"),
            Code = ReadString(order, "code"),
            Status = ReadString(order, "status"),
            Ecommerce = ReadString(order, "ecommerce"),
            CreatedAt = ReadDateTimeOffset(order, "createdAt"),
            CollectedAt = ReadDateTimeOffset(order, "collectedAt"),
            CompletedAt = ReadDateTimeOffset(order, "completedAt"),
            CanceledAt = ReadDateTimeOffset(order, "canceledAt")
        };

        _logger.LogInformation(
            "J3 operation {Operation} succeeded with HTTP {StatusCode}",
            OpTracking,
            200);
        return mapped;
    }

    private void EnsureCanCall(string operationName)
    {
        // Enabled=false não impede startup; validação só na invocação.
        if (string.IsNullOrWhiteSpace(_options.GraphQlUrl))
            throw new J3ApiException(operationName, "J3 GraphQL URL is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Token))
            throw new J3ApiException(operationName, "J3 token is not configured.");
    }

    private async Task<JsonDocument> PostGraphQlAsync(
        string operationName,
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.GraphQlUrl!.Trim(), UriKind.Absolute, out var endpoint))
            throw new J3ApiException(operationName, "J3 GraphQL URL is invalid.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var companyGroup = string.IsNullOrWhiteSpace(_options.CompanyGroupCode)
            ? "J3"
            : _options.CompanyGroupCode.Trim();
        request.Headers.TryAddWithoutValidation("x-company-group-code", companyGroup);

        var body = JsonSerializer.Serialize(new { query, variables }, JsonOptions);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("J3 operation {Operation} failed: timeout", operationName);
            throw new J3ApiException(operationName, $"J3 {operationName}: request timed out.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("J3 operation {Operation} failed: timeout", operationName);
            throw new J3ApiException(operationName, $"J3 {operationName}: request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "J3 operation {Operation} failed: network error", operationName);
            throw new J3ApiException(
                operationName,
                $"J3 {operationName}: network error.",
                innerException: null);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "J3 operation {Operation} failed with HTTP {StatusCode}",
                    operationName,
                    status);
                throw new J3ApiException(
                    operationName,
                    $"J3 {operationName}: HTTP {status}.",
                    httpStatus: status);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "J3 operation {Operation} failed: invalid JSON", operationName);
                throw new J3ApiException(
                    operationName,
                    $"J3 {operationName}: invalid JSON response.",
                    httpStatus: status);
            }

            if (doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var codes = ExtractGraphQlCodes(errors);
                _logger.LogWarning(
                    "J3 operation {Operation} failed with HTTP {StatusCode} (GraphQL errors)",
                    operationName,
                    status);
                doc.Dispose();
                throw new J3ApiException(
                    operationName,
                    $"J3 {operationName}: GraphQL errors.",
                    httpStatus: status,
                    graphQlErrorCodes: codes);
            }

            return doc;
        }
    }

    private static IReadOnlyList<string>? ExtractGraphQlCodes(JsonElement errors)
    {
        var list = new List<string>();
        foreach (var err in errors.EnumerateArray())
        {
            if (err.ValueKind != JsonValueKind.Object)
                continue;
            if (err.TryGetProperty("extensions", out var ext)
                && ext.ValueKind == JsonValueKind.Object
                && ext.TryGetProperty("code", out var codeEl)
                && codeEl.ValueKind == JsonValueKind.String)
            {
                var c = codeEl.GetString();
                if (!string.IsNullOrWhiteSpace(c))
                    list.Add(c);
            }
            else if (err.TryGetProperty("code", out var topCode)
                     && topCode.ValueKind == JsonValueKind.String)
            {
                var c = topCode.GetString();
                if (!string.IsNullOrWhiteSpace(c))
                    list.Add(c);
            }
        }

        return list.Count == 0 ? null : list;
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (p.ValueKind != JsonValueKind.String)
            return null;
        var s = p.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }
}
