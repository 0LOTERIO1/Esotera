using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
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

        if (!_options.HasValidBaseUrl)
        {
            _logger.LogWarning("Melhor Envio calculate: base URL inválida");
            return new MelhorEnvioCalculateOutcome { Ok = false };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CalculateUrl);
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

    public async Task<MelhorEnvioCartOutcome> CreateCartItemAsync(
        MelhorEnvioCartRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UserAgent))
        {
            _logger.LogWarning("Melhor Envio cart: User-Agent ausente");
            return Fail(MelhorEnvioShipmentErrorCodes.NotConfigured, "User-Agent não configurado.");
        }

        if (!_options.HasValidBaseUrl)
        {
            _logger.LogWarning("Melhor Envio cart: base URL inválida");
            return Fail(MelhorEnvioShipmentErrorCodes.NotConfigured, "Base URL inválida.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CartUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent.Trim());

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(BuildCartBody(request)),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Melhor Envio cart: timeout");
            return new MelhorEnvioCartOutcome
            {
                TimedOut = true,
                ErrorCode = MelhorEnvioShipmentErrorCodes.Timeout,
                ErrorMessage = "Tempo esgotado ao inserir no carrinho. Resultado desconhecido."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Melhor Envio cart: erro de rede");
            return new MelhorEnvioCartOutcome
            {
                NetworkError = true,
                ErrorCode = MelhorEnvioShipmentErrorCodes.NetworkError,
                ErrorMessage = "Falha de rede ao contatar o Melhor Envio."
            };
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("Melhor Envio cart: 401 Unauthenticated");
                return new MelhorEnvioCartOutcome
                {
                    Unauthenticated = true,
                    ErrorCode = MelhorEnvioShipmentErrorCodes.Unauthenticated,
                    ErrorMessage = "Token recusado pelo Melhor Envio."
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Token válido, permissão ausente — tipicamente falta cart-write.
                _logger.LogWarning("Melhor Envio cart: 403 Forbidden (provável escopo ausente)");
                return new MelhorEnvioCartOutcome
                {
                    Forbidden = true,
                    ErrorCode = MelhorEnvioShipmentErrorCodes.Forbidden,
                    ErrorMessage =
                        "Reautorize o Melhor Envio com os novos escopos antes de criar envio."
                };
            }

            // Corpo lido só para extrair mensagem de validação — nunca logado inteiro.
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var isValidation = statusCode is >= 400 and < 500;
                _logger.LogWarning("Melhor Envio cart: HTTP {StatusCode}", statusCode);
                return new MelhorEnvioCartOutcome
                {
                    ValidationRejected = isValidation,
                    ErrorCode = isValidation
                        ? MelhorEnvioShipmentErrorCodes.ValidationRejected
                        : MelhorEnvioShipmentErrorCodes.Http(statusCode),
                    ErrorMessage = MelhorEnvioShipmentErrorCodes.SanitizeMessage(
                        ExtractErrorMessage(raw) ?? $"Melhor Envio respondeu HTTP {statusCode}.")
                };
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var id = ReadString(doc.RootElement, "id");
                var protocol = ReadString(doc.RootElement, "protocol");

                if (string.IsNullOrWhiteSpace(id))
                {
                    _logger.LogWarning("Melhor Envio cart: resposta 2xx sem id");
                    return Fail(
                        MelhorEnvioShipmentErrorCodes.ResponseWithoutId,
                        "Melhor Envio respondeu sem o id do envio. Verifique o painel antes de repetir.");
                }

                _logger.LogInformation(
                    "Melhor Envio cart: envio inserido no carrinho (protocol={Protocol})",
                    protocol);

                return new MelhorEnvioCartOutcome
                {
                    Ok = true,
                    ShipmentId = id,
                    Protocol = protocol
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Melhor Envio cart: JSON inválido");
                return Fail(
                    MelhorEnvioShipmentErrorCodes.InvalidJson,
                    "Resposta ilegível do Melhor Envio. Verifique o painel antes de repetir.");
            }
        }
    }

    private static MelhorEnvioCartOutcome Fail(string code, string message) =>
        new() { ErrorCode = code, ErrorMessage = message };

    /// <summary>Serializa no formato snake_case exigido pela API.</summary>
    internal static object BuildCartBody(MelhorEnvioCartRequest r) => new
    {
        service = r.Service,
        from = BuildParty(r.From),
        to = BuildParty(r.To),
        products = r.Products.Select(p => new
        {
            name = p.Name,
            quantity = p.Quantity.ToString(CultureInfo.InvariantCulture),
            unitary_value = p.UnitaryValue
        }).ToArray(),
        volumes = r.Volumes.Select(v => new
        {
            height = v.HeightCm,
            width = v.WidthCm,
            length = v.LengthCm,
            weight = v.WeightKg
        }).ToArray(),
        options = new
        {
            platform = r.Options.Platform,
            reminder = r.Options.Reminder,
            insurance_value = r.Options.InsuranceValue,
            receipt = r.Options.Receipt,
            own_hand = r.Options.OwnHand,
            reverse = r.Options.Reverse,
            non_commercial = r.Options.NonCommercial,
            invoice = string.IsNullOrWhiteSpace(r.Options.InvoiceKey)
                ? null
                : new { key = r.Options.InvoiceKey },
            tags = string.IsNullOrWhiteSpace(r.Options.OrderTag)
                ? null
                : new[] { new { tag = r.Options.OrderTag, url = (string?)null } }
        }
    };

    private static object BuildParty(MelhorEnvioCartParty p) => new
    {
        name = p.Name,
        email = p.Email,
        phone = p.Phone,
        document = p.Document,
        company_document = p.CompanyDocument,
        state_register = p.StateRegister,
        economic_activity_code = p.EconomicActivityCode,
        address = p.Address,
        complement = p.Complement,
        number = p.Number,
        district = p.District,
        city = p.City,
        postal_code = p.PostalCode,
        state_abbr = p.StateAbbr,
        country_id = p.CountryId
    };

    /// <summary>Extrai mensagem de erro sem devolver o payload inteiro.</summary>
    internal static string? ExtractErrorMessage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                var message = msg.GetString();
                var firstError = FirstValidationError(root);
                return firstError is null ? message : $"{message} ({firstError})";
            }

            return FirstValidationError(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FirstValidationError(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in errors.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in prop.Value.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.String)
                        return $"{prop.Name}: {entry.GetString()}";
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                return $"{prop.Name}: {prop.Value.GetString()}";
            }
        }

        return null;
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
