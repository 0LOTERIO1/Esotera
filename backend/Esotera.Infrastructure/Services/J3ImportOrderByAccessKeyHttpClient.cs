using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// HTTP client para importOrderByAccessKey. Sem Polly/retry. Sem createTmsOrders.
/// </summary>
public sealed class J3ImportOrderByAccessKeyHttpClient : IJ3ImportOrderByAccessKeyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly J3ShippingOptions _options;
    private readonly ILogger<J3ImportOrderByAccessKeyHttpClient> _logger;

    public J3ImportOrderByAccessKeyHttpClient(
        HttpClient http,
        IOptions<J3ShippingOptions> options,
        ILogger<J3ImportOrderByAccessKeyHttpClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<J3CreateOrderAttemptResult> ImportAsync(
        Order order,
        FiscalInvoiceParseResult parsedFiscal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(parsedFiscal);

        if (!_options.ImportByAccessKeyEnabled)
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.ImportByAccessKeyDisabled);

        if (!_options.HasValidGraphQlUrl || string.IsNullOrWhiteSpace(_options.Token))
            return LocalFailure(order.Id, J3FulfillmentErrorCodes.Configuration);

        var built = J3ImportOrderByAccessKeyMapper.TryBuild(order, parsedFiscal, _options);
        if (!built.IsValid || built.Command is null)
            return LocalFailure(order.Id, built.ErrorCode ?? J3FulfillmentErrorCodes.Configuration);

        return await SendOnceAsync(built.Command, cancellationToken);
    }

    private J3CreateOrderAttemptResult LocalFailure(Guid orderId, string errorCode)
    {
        var sanitized = J3FulfillmentErrorCodes.Sanitize(errorCode) ?? J3FulfillmentErrorCodes.Configuration;
        _logger.LogInformation(
            "J3 operation {Operation} outcome {Outcome} order {OrderId} error {ErrorCode} (no HTTP)",
            J3ImportOrderByAccessKeyMutation.OperationName,
            J3CreateOrderOutcome.DefiniteFailure,
            orderId,
            sanitized);
        return J3CreateOrderAttemptResult.DefiniteFailure(sanitized);
    }

    private async Task<J3CreateOrderAttemptResult> SendOnceAsync(
        J3ImportOrderByAccessKeyCommand command,
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
            query = J3ImportOrderByAccessKeyMutation.Document,
            operationName = J3ImportOrderByAccessKeyMutation.OperationName,
            variables = new { input = command.Input }
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
            var code = ex is OperationCanceledException or TaskCanceledException
                ? J3FulfillmentErrorCodes.TimeoutUnknown
                : J3FulfillmentErrorCodes.NetworkUnknown;
            _logger.LogWarning(
                "J3 operation {Operation} outcome {Outcome} order {OrderId} error {ErrorCode} durationMs {DurationMs}",
                J3ImportOrderByAccessKeyMutation.OperationName,
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
                LogOutcome(command.LocalOrderId, J3CreateOrderOutcome.UnknownOutcome,
                    J3FulfillmentErrorCodes.FromHttpStatus(status), status, sw.ElapsedMilliseconds, null);
                return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.FromHttpStatus(status));
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                LogOutcome(command.LocalOrderId, J3CreateOrderOutcome.UnknownOutcome,
                    J3FulfillmentErrorCodes.JsonInvalid, status, sw.ElapsedMilliseconds, null);
                return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.JsonInvalid);
            }

            using (doc)
            {
                return Classify(command.LocalOrderId, doc, status, sw.ElapsedMilliseconds);
            }
        }
    }

    private J3CreateOrderAttemptResult Classify(Guid orderId, JsonDocument doc, int status, long durationMs)
    {
        var root = doc.RootElement;
        if (root.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            var insights = J3GraphQlErrorClassifier.Extract(errors);
            LogGraphqlErrors(insights);

            if (J3GraphQlErrorClassifier.AllAuthRejected(errors))
            {
                var authCode = J3GraphQlErrorClassifier.AuthFailureCode(insights);
                LogOutcome(orderId, J3CreateOrderOutcome.DefiniteFailure, authCode, status, durationMs, insights);
                return J3CreateOrderAttemptResult.DefiniteFailure(authCode);
            }

            if (J3GraphQlErrorClassifier.AllPreExecution(errors)
                || status == 400)
            {
                LogOutcome(orderId, J3CreateOrderOutcome.DefiniteFailure,
                    J3FulfillmentErrorCodes.GraphqlValidation, status, durationMs, insights);
                return J3CreateOrderAttemptResult.DefiniteFailure(J3FulfillmentErrorCodes.GraphqlValidation);
            }

            var ambiguous = J3GraphQlErrorClassifier.PrimaryErrorCode(insights);
            if (ambiguous == J3FulfillmentErrorCodes.GraphqlAmbiguous
                || !string.Equals(ambiguous, J3FulfillmentErrorCodes.GraphqlValidation, StringComparison.Ordinal))
            {
                // Prefer explicit GraphQL code when present; still UnknownOutcome for non-auth/non-validation.
                LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome,
                    ambiguous, status, durationMs, insights);
                return J3CreateOrderAttemptResult.Unknown(ambiguous);
            }

            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome,
                J3FulfillmentErrorCodes.GraphqlAmbiguous, status, durationMs, insights);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.GraphqlAmbiguous);
        }

        if (status >= 500)
        {
            var code5 = J3FulfillmentErrorCodes.FromHttpStatus(status);
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, code5, status, durationMs, null);
            return J3CreateOrderAttemptResult.Unknown(code5);
        }

        if (status is >= 400 and < 500)
        {
            var code4 = J3FulfillmentErrorCodes.FromHttpStatus(status);
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, code4, status, durationMs, null);
            return J3CreateOrderAttemptResult.Unknown(code4);
        }

        if (!root.TryGetProperty("data", out var data)
            || data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || !data.TryGetProperty("importOrderByAccessKey", out var result)
            || result.ValueKind != JsonValueKind.Object)
        {
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome,
                J3FulfillmentErrorCodes.Unknown, status, durationMs, null);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Unknown);
        }

        if (!result.TryGetProperty("success", out var successEl)
            || successEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome,
                J3FulfillmentErrorCodes.Unknown, status, durationMs, null);
            return J3CreateOrderAttemptResult.Unknown(J3FulfillmentErrorCodes.Unknown);
        }

        if (successEl.GetBoolean())
        {
            LogOutcome(orderId, J3CreateOrderOutcome.Success, null, status, durationMs, null);
            return J3CreateOrderAttemptResult.Success(
                orderId: "imported",
                orderCode: null,
                trackingNumber: null,
                deliveryPointId: null);
        }

        // success=false: sem prova de zero criação.
        var apiCode = ReadSanitizedApiError(result) ?? J3FulfillmentErrorCodes.SuccessFalse;
        LogOutcome(orderId, J3CreateOrderOutcome.UnknownOutcome, apiCode, status, durationMs, null);
        return J3CreateOrderAttemptResult.Unknown(apiCode);
    }

    private void LogGraphqlErrors(IReadOnlyList<J3GraphQlErrorClassifier.Insight> insights)
    {
        foreach (var i in insights)
        {
            _logger.LogWarning(
                "J3 GraphQL error code {GraphqlErrorCode} message {GraphqlErrorMessage}",
                J3FulfillmentErrorCodes.Sanitize(i.Code) ?? "UNKNOWN",
                i.SanitizedMessage ?? "");
        }
    }

    private void LogOutcome(
        Guid orderId,
        J3CreateOrderOutcome outcome,
        string? errorCode,
        int status,
        long durationMs,
        IReadOnlyList<J3GraphQlErrorClassifier.Insight>? insights)
    {
        _logger.LogInformation(
            "J3 operation {Operation} outcome {Outcome} order {OrderId} HTTP {StatusCode} error {ErrorCode} durationMs {DurationMs}",
            J3ImportOrderByAccessKeyMutation.OperationName,
            outcome,
            orderId,
            status,
            errorCode,
            durationMs);

        if (insights is { Count: > 0 })
            LogGraphqlErrors(insights);
    }

    private static string? ReadSanitizedApiError(JsonElement result)
    {
        if (!result.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
            return null;
        if (!error.TryGetProperty("errorCode", out var code) || code.ValueKind != JsonValueKind.String)
            return null;
        return J3FulfillmentErrorCodes.Sanitize(code.GetString());
    }
}
