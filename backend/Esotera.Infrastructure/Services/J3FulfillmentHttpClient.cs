using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

    /// <summary>
    /// Cliente HTTP da mutation createTmsOrders (Pedido Avulso). Sem Polly / sem retry.
    /// Exatamente 1 input por POST. Gate mutativo: J3_FULFILLMENT_ENABLED (não J3_ENABLED).
    /// DefiniteFailure só antes de SendAsync, ou GraphQL parse/validation inequívoco (pré-resolver).
    /// Após SendAsync: Success só com success=true + orderId; demais → UnknownOutcome (inclui 401/403 e success=false).
    /// </summary>
public sealed class J3FulfillmentHttpClient : IJ3FulfillmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Códigos GraphQL padrão de documento inválido (spec / Apollo / HotChocolate):
    /// o servidor recusa o documento antes de executar resolvers.
    /// Só tratamos como DefiniteFailure se TODOS os errors[] tiverem exclusivamente estes códigos.
    /// Mensagem "validation" genérica ou código ausente → UnknownOutcome.
    /// </summary>
    private static readonly HashSet<string> GraphqlPreExecutionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "GRAPHQL_PARSE_FAILED",
        "GRAPHQL_VALIDATION_FAILED"
    };

    private readonly HttpClient _http;
    private readonly J3ShippingOptions _options;
    private readonly ILogger<J3FulfillmentHttpClient> _logger;

    public J3FulfillmentHttpClient(
        HttpClient http,
        IOptions<J3ShippingOptions> options,
        ILogger<J3FulfillmentHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<J3CreateOrderAttemptResult> CreateOrderAsync(
        Order order,
        StoreSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);

        if (!_options.FulfillmentEnabled)
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.FulfillmentDisabled);

        if (string.IsNullOrWhiteSpace(_options.SellerId))
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.MissingSellerId);

        if (string.IsNullOrWhiteSpace(_options.SellerInformationId))
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.MissingSellerInformationId);

        if (!_options.HasValidGraphQlUrl || string.IsNullOrWhiteSpace(_options.Token))
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.Configuration);

        var built = J3CreateTmsOrderMapper.TryBuild(order, settings, _options);
        if (!built.IsValid || built.Command is null)
            return LocalFailure(order.Id, built.ErrorCode ?? J3FulfillmentErrorCodes.Configuration);

        return await SendOnceAsync(built.Command, cancellationToken);
    }

    private J3CreateOrderAttemptResult LocalFailure(Guid orderId, string errorCode)
    {
        var sanitized = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Configuration;
        _logger.LogInformation(
            "J3 operation {Operation} outcome {Outcome} order {OrderId} error {ErrorCode} (no HTTP)",
            J3CreateTmsOrderMutation.OperationName,
            J3CreateOrderOutcome.DefiniteFailure,
            orderId,
            sanitized);
        return J3CreateOrderAttemptResult.DefiniteFailure(sanitized);
    }

    private async Task<J3CreateOrderAttemptResult> SendOnceAsync(
        J3CreateTmsOrderCommand command,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(_options.GraphQlUrl!.Trim(), UriKind.Absolute);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var companyGroup = string.IsNullOrWhiteSpace(_options.CompanyGroupCode)
            ? "J3"
            : _options.CompanyGroupCode.Trim();
        request.Headers.TryAddWithoutValidation("x-company-group-code", companyGroup);

        var payload = new
        {
            query = J3CreateTmsOrderMutation.Document,
            operationName = J3CreateTmsOrderMutation.OperationName,
            variables = new
            {
                inputs = new[] { command.Input }
            }
        };
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var code = ClassifyTransportError(ex);
            _logger.LogWarning(
                "J3 operation {Operation} outcome {Outcome} order {OrderId} error {ErrorCode} durationMs {DurationMs}",
                J3CreateTmsOrderMutation.OperationName,
                J3CreateOrderOutcome.UnknownOutcome,
                command.LocalOrderId,
                code,
                sw.ElapsedMilliseconds);
            return J3CreateOrderAttemptResult.Unknown(code);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            sw.Stop();

            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception)
            {
                var readCode = J3FulfillmentErrorCodes.FromHttpStatus(status);
                LogOutcome(command.LocalOrderId, J3CreateOrderOutcome.UnknownOutcome, readCode, status, sw.ElapsedMilliseconds);
                return J3CreateOrderAttemptResult.Unknown(readCode);
            }

            if (status == 400)
                return ClassifyHttp400(command.LocalOrderId, body, sw.ElapsedMilliseconds);

            if (status >= 500 || status == (int)HttpStatusCode.BadGateway
                || status == (int)HttpStatusCode.ServiceUnavailable
                || status == (int)HttpStatusCode.GatewayTimeout)
            {
                var code5xx = J3FulfillmentErrorCodes.FromHttpStatus(status);
                LogOutcome(command.LocalOrderId, J3CreateOrderOutcome.UnknownOutcome, code5xx, status, sw.ElapsedMilliseconds);
                return J3CreateOrderAttemptResult.Unknown(code5xx);
            }

            if (status is >= 400 and < 500)
            {
                var code4xx = J3FulfillmentErrorCodes.FromHttpStatus(status);
                LogOutcome(command.LocalOrderId, J3CreateOrderOutcome.UnknownOutcome, code4xx, status, sw.ElapsedMilliseconds);
                return J3CreateOrderAttemptResult.Unknown(code4xx);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                LogOutcome(
                    command.LocalOrderId,
                    J3CreateOrderOutcome.UnknownOutcome,
                    J3FulfillmentErrorCodes.JsonInvalid,
                    status,
                    sw.ElapsedMilliseconds);
                return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.JsonInvalid);
            }

            using (doc)
            {
                return ClassifyGraphQlDocument(command.LocalOrderId, doc, status, sw.ElapsedMilliseconds);
            }
        }
    }

    private J3CreateOrderAttemptResult ClassifyHttp400(Guid orderId, string body, long durationMs)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0
                && AllPreExecution(errors))
            {
                LogOutcome(orderId, J3CreateOrderOutcome.DefiniteFailure, J3FulfillmentErrorCodes.GraphqlValidation, 400, durationMs);
                return J3CreateOrderAttemptResult.DefiniteFailure(J3FulfillmentErrorCodes.GraphqlValidation);
            }
        }
        catch (JsonException)
        {
            // 400 com body ilegível: não prova rejeição pré-execução.
        }

        LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, J3FulfillmentErrorCodes.FromHttpStatus(400), 400, durationMs);
        return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.FromHttpStatus(400));
    }

    private J3CreateOrderAttemptResult ClassifyGraphQlDocument(
        Guid orderId,
        JsonDocument doc,
        int status,
        long durationMs)
    {
        var root = doc.RootElement;
        if (root.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            if (AllPreExecution(errors))
            {
                LogOutcome(orderId, J3CreateOrderOutcome.DefiniteFailure, J3FulfillmentErrorCodes.GraphqlValidation, status, durationMs);
                return J3CreateOrderAttemptResult.DefiniteFailure(J3FulfillmentErrorCodes.GraphqlValidation);
            }

            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, J3FulfillmentErrorCodes.GraphqlAmbiguous, status, durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.GraphqlAmbiguous);
        }

        if (!root.TryGetProperty("data", out var data)
            || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || !data.TryGetProperty("createTmsOrders", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, J3FulfillmentErrorCodes.Unknown, status, durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Unknown);
        }

        if (results.GetArrayLength() != 1)
        {
            LogOutcome(
                orderId,
                J3CreateOrderOutcome.UnknownOutcome,
                J3FulfillmentErrorCodes.UnexpectedResultCount,
                status,
                durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.UnexpectedResultCount);
        }

        var created = results[0];
        if (created.ValueKind != JsonValueKind.Object)
        {
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, J3FulfillmentErrorCodes.Unknown, status, durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Unknown);
        }

        if (created.TryGetProperty("index", out var indexEl)
            && indexEl.ValueKind == JsonValueKind.Number
            && indexEl.TryGetInt32(out var index)
            && index != 0)
        {
            LogOutcome(
                orderId,
                J3CreateOrderOutcome.UnknownOutcome,
                J3FulfillmentErrorCodes.UnexpectedIndex,
                status,
                durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.UnexpectedIndex);
        }

        if (!created.TryGetProperty("success", out var successEl)
            || successEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, J3FulfillmentErrorCodes.Unknown, status, durationMs);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Unknown);
        }

        var j3OrderId = ReadString(created, "orderId");
        var sanitizedApiError = ReadSanitizedApiErrorCode(created);

        if (successEl.GetBoolean())
        {
            if (string.IsNullOrWhiteSpace(j3OrderId))
            {
                LogOutcome(
                    orderId,
                    J3CreateOrderOutcome.UnknownOutcome,
                    J3FulfillmentErrorCodes.SuccessWithoutOrderId,
                    status,
                    durationMs);
                return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.SuccessWithoutOrderId);
            }

            LogOutcome(orderId, J3CreateOrderOutcome.Success, null, status, durationMs);
            return J3CreateOrderAttemptResult.Success(j3OrderId, orderCode: null, trackingNumber: null, deliveryPointId: null);
        }

        // Sem contrato J3 de que success=false implica zero criação. Não logar error.description.
        var falseCode = sanitizedApiError ?? J3FulfillmentErrorCodes.SuccessFalse;
        LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, falseCode, status, durationMs);
        return J3CreateOrderAttemptResult.Unknown(falseCode);
    }

    private void LogOutcome(Guid orderId, J3CreateOrderOutcome outcome, string? errorCode, int status, long durationMs)
    {
        _logger.LogInformation(
            "J3 operation {Operation} outcome {Outcome} order {OrderId} HTTP {StatusCode} error {ErrorCode} durationMs {DurationMs}",
            J3CreateTmsOrderMutation.OperationName,
            outcome,
            orderId,
            status,
            errorCode,
            durationMs);
    }

    private static string ClassifyTransportError(Exception ex) =>
        ex is OperationCanceledException or TaskCanceledException
            ? J3FulfillmentErrorCodes.TimeoutUnknown
            : J3FulfillmentErrorCodes.NetworkUnknown;

    private static bool AllPreExecution(JsonElement errors)
    {
        var any = false;
        foreach (var err in errors.EnumerateArray())
        {
            any = true;
            var code = ReadGraphQlCode(err);
            if (code is null || !GraphqlPreExecutionCodes.Contains(code))
                return false;
        }

        return any;
    }

    private static string? ReadGraphQlCode(JsonElement err)
    {
        if (err.ValueKind != JsonValueKind.Object)
            return null;
        if (err.TryGetProperty("extensions", out var ext)
            && ext.ValueKind == JsonValueKind.Object
            && ext.TryGetProperty("code", out var codeEl)
            && codeEl.ValueKind == JsonValueKind.String)
        {
            return codeEl.GetString();
        }

        if (err.TryGetProperty("code", out var top)
            && top.ValueKind == JsonValueKind.String)
        {
            return top.GetString();
        }

        return null;
    }

    private static string? ReadSanitizedApiErrorCode(JsonElement created)
    {
        if (!created.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            return null;
        var raw = ReadString(error, "errorCode");
        return J3FulfillmentErrorCodes.Sanitize(raw);
    }

    private static string? ReadString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }
}
