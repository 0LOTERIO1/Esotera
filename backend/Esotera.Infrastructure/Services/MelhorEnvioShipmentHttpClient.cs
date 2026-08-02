using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

public sealed class MelhorEnvioShipmentHttpClient : IMelhorEnvioShipmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly MelhorEnvioOptions _options;
    private readonly ILogger<MelhorEnvioShipmentHttpClient> _logger;

    public MelhorEnvioShipmentHttpClient(
        HttpClient http,
        IOptions<MelhorEnvioOptions> options,
        ILogger<MelhorEnvioShipmentHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MelhorEnvioCalculateOutcome> CalculateAsync(
        MelhorEnvioCalculateRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            _logger.LogWarning("Melhor Envio calculate: User-Agent ausente");
            return new MelhorEnvioCalculateOutcome { Ok = false };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, MelhorEnvioOptions.SandboxCalculateUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent.Trim());

        var body = new
        {
            from = new { postal_code = request.FromPostalCode },
            to = new { postal_code = request.ToPostalCode },
            package = new
            {
                height = request.HeightCm,
                width = request.WidthCm,
                length = request.LengthCm,
                weight = request.WeightKg
            },
            services = request.Services
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Melhor Envio calculate: timeout");
            return new MelhorEnvioCalculateOutcome { Ok = false, TimedOut = true };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Melhor Envio calculate: timeout");
            return new MelhorEnvioCalculateOutcome { Ok = false, TimedOut = true };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Melhor Envio calculate: erro de rede");
            return new MelhorEnvioCalculateOutcome { Ok = false, NetworkError = true };
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Melhor Envio calculate: 401 Unauthenticated");
                return new MelhorEnvioCalculateOutcome { Ok = false, Unauthenticated = true };
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Melhor Envio calculate: HTTP {StatusCode}",
                    (int)response.StatusCode);
                return new MelhorEnvioCalculateOutcome { Ok = false };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var services = ParseServices(doc.RootElement);
                return new MelhorEnvioCalculateOutcome { Ok = true, Services = services };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Melhor Envio calculate: JSON inválido");
                return new MelhorEnvioCalculateOutcome { Ok = false };
            }
        }
    }

    internal static IReadOnlyList<MelhorEnvioRawServiceQuote> ParseServices(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return Array.Empty<MelhorEnvioRawServiceQuote>();

        var list = new List<MelhorEnvioRawServiceQuote>();
        foreach (var item in root.EnumerateArray())
        {
            list.Add(new MelhorEnvioRawServiceQuote
            {
                CompanyId = ReadNestedInt(item, "company", "id") ?? ReadInt(item, "company_id"),
                CompanyName = ReadNestedString(item, "company", "name"),
                ServiceId = ReadInt(item, "id"),
                ServiceName = ReadString(item, "name"),
                Price = ReadDecimal(item, "price"),
                CustomPrice = ReadDecimal(item, "custom_price"),
                DeliveryTime = ReadInt(item, "delivery_time"),
                CustomDeliveryTime = ReadInt(item, "custom_delivery_time"),
                Error = ReadError(item)
            });
        }

        return list;
    }

    private static string? ReadError(JsonElement item)
    {
        if (!item.TryGetProperty("error", out var err))
            return null;
        return err.ValueKind switch
        {
            JsonValueKind.String => err.GetString(),
            JsonValueKind.Object when err.TryGetProperty("message", out var msg) => msg.GetString(),
            JsonValueKind.Null => null,
            _ => err.ToString()
        };
    }

    private static int? ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var s))
            return s;
        return null;
    }

    private static int? ReadNestedInt(JsonElement el, string parent, string child)
    {
        if (!el.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object)
            return null;
        return ReadInt(p, child);
    }

    private static string? ReadNestedString(JsonElement el, string parent, string child)
    {
        if (!el.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object)
            return null;
        return ReadString(p, child);
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        return p.GetString();
    }

    private static decimal? ReadDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                p.GetString(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var s))
            return s;
        return null;
    }
}
