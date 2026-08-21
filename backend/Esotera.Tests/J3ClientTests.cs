using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Esotera.Application.Exceptions;
using Esotera.Application.Options;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// Testes do J3Client real via HttpMessageHandler mock — zero rede real.
/// Host de produção (web.api.j3tms.com.br) é bloqueado estruturalmente no handler.
/// </summary>
public class J3ClientTests
{
    private const string FakeToken = "fake-j3-token-for-tests";
    private const string TestGraphQlUrl = "http://localhost/j3-graphql-test/";
    private const string ProductionHostMarker = "j3tms.com.br";

    [Fact]
    public async Task A_Coverage_True_ReturnsTrue()
    {
        var client = CreateClient(_ => OkJson("""{"data":{"isValidServiceArea":true}}"""));
        var result = await client.IsServiceAreaAsync("01310-100");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task B_Coverage_False_ReturnsFalse()
    {
        var client = CreateClient(_ => OkJson("""{"data":{"isValidServiceArea":false}}"""));
        var result = await client.IsServiceAreaAsync("01310100");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task C_Coverage_SendsMaskedZipInPayload()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        await client.IsServiceAreaAsync("01310-100");

        using var doc = JsonDocument.Parse(body!);
        var zip = doc.RootElement
            .GetProperty("variables")
            .GetProperty("input")
            .GetProperty("zipcode")
            .GetString();
        zip.Should().Be("01310-100");
        doc.RootElement.GetProperty("operationName").GetString().Should().Be("IsValidServiceArea");
        doc.RootElement.TryGetProperty("query", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("variables", out _).Should().BeTrue();
        body.Should().Contain("isValidServiceArea");
    }

    [Fact]
    public async Task Coverage_DigitsOnly_SendsMaskedZip_03065000()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        await client.IsServiceAreaAsync("03065000");

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement
            .GetProperty("variables")
            .GetProperty("input")
            .GetProperty("zipcode")
            .GetString()
            .Should().Be("03065-000");
    }

    [Fact]
    public async Task Coverage_AlreadyMasked_SendsSameMaskedZip()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        await client.IsServiceAreaAsync("03065-000");

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement
            .GetProperty("variables")
            .GetProperty("input")
            .GetProperty("zipcode")
            .GetString()
            .Should().Be("03065-000");
    }

    [Fact]
    public async Task Coverage_InvalidCep_DoesNotSendHttp()
    {
        var httpCalls = 0;
        var client = CreateClient(_ =>
        {
            httpCalls++;
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        var act = () => client.IsServiceAreaAsync("123");
        await act.Should().ThrowAsync<J3ApiException>();
        httpCalls.Should().Be(0);
    }

    [Fact]
    public async Task Coverage_EmptyCep_DoesNotSendHttp()
    {
        var httpCalls = 0;
        var client = CreateClient(_ =>
        {
            httpCalls++;
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        var act = () => client.IsServiceAreaAsync("abc");
        await act.Should().ThrowAsync<J3ApiException>();
        httpCalls.Should().Be(0);
    }

    [Fact]
    public async Task D_Coverage_SendsCompanyGroupCodeFromOptions()
    {
        string? body = null;
        var client = CreateClient(
            req =>
            {
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return OkJson("""{"data":{"isValidServiceArea":true}}""");
            },
            companyGroupCode: "CUSTOM_GROUP");

        await client.IsServiceAreaAsync("01310100");

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement
            .GetProperty("variables")
            .GetProperty("input")
            .GetProperty("companyGroupCode")
            .GetString()
            .Should().Be("CUSTOM_GROUP");
    }

    [Fact]
    public async Task E_Authorization_BearerConfigured_WithoutPrintingToken()
    {
        AuthenticationHeaderValue? auth = null;
        var client = CreateClient(req =>
        {
            auth = req.Headers.Authorization;
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        });

        await client.IsServiceAreaAsync("01310100");

        auth.Should().NotBeNull();
        auth!.Scheme.Should().Be("Bearer");
        // Assert equality with known fake — never WriteLine / dump header value.
        auth.Parameter.Should().Be(FakeToken);
        auth.ToString().Should().StartWith("Bearer ");
    }

    [Fact]
    public async Task F_XCompanyGroupCode_HeaderSent()
    {
        string? header = null;
        var client = CreateClient(
            req =>
            {
                if (req.Headers.TryGetValues("x-company-group-code", out var values))
                    header = values.FirstOrDefault();
                return OkJson("""{"data":{"isValidServiceArea":true}}""");
            },
            companyGroupCode: "J3");

        await client.IsServiceAreaAsync("01310100");
        header.Should().Be("J3");
    }

    [Fact]
    public async Task G_Http401_ThrowsSanitizedJ3ApiException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":"unauthorized","token":"SHOULD_NOT_LEAK"}""", Encoding.UTF8, "application/json")
        });

        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.OperationName.Should().Be("IsValidServiceArea");
        ex.Which.HttpStatus.Should().Be(401);
        ex.Which.Message.Should().NotContain(FakeToken);
        ex.Which.Message.Should().NotContain("SHOULD_NOT_LEAK");
        ex.Which.Message.Should().NotContain("Bearer");
    }

    [Fact]
    public async Task H_Http500_ThrowsSanitizedJ3ApiException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("internal", Encoding.UTF8, "text/plain")
        });

        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.HttpStatus.Should().Be(500);
        ex.Which.Message.Should().NotContain(FakeToken);
    }

    [Fact]
    public async Task I_GraphQlErrors_Http200_ThrowsFailure_NotFalse()
    {
        var payload = """
            {
              "errors": [
                { "message": "boom", "extensions": { "code": "INTERNAL" } }
              ],
              "data": { "isValidServiceArea": false }
            }
            """;
        var client = CreateClient(_ => OkJson(payload));

        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.OperationName.Should().Be("IsValidServiceArea");
        ex.Which.HttpStatus.Should().Be(200);
        ex.Which.GraphQlErrorCodes.Should().Contain("INTERNAL");
        ex.Which.Message.Should().NotContain(FakeToken);
    }

    [Fact]
    public async Task J_InvalidJson_ThrowsSafeFailure()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        });

        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.Message.Should().Contain("invalid JSON");
        ex.Which.Message.Should().NotContain(FakeToken);
    }

    [Fact]
    public async Task K_Tracking_Valid_ParsesAllFields()
    {
        var payload = """
            {
              "data": {
                "getTrackingOrderSeller": {
                  "id": "ord-1",
                  "code": "CODE-1",
                  "status": "IN_TRANSIT",
                  "ecommerce": "esotera",
                  "createdAt": "2026-01-02T03:04:05Z",
                  "collectedAt": "2026-01-03T04:05:06Z",
                  "completedAt": "2026-01-04T05:06:07Z",
                  "canceledAt": "2026-01-05T06:07:08Z"
                }
              }
            }
            """;
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkJson(payload);
        });

        var result = await client.GetTrackingAsync("TRK-123");
        result.Should().NotBeNull();
        result!.Id.Should().Be("ord-1");
        result.Code.Should().Be("CODE-1");
        result.Status.Should().Be("IN_TRANSIT");
        result.Ecommerce.Should().Be("esotera");
        result.CreatedAt.Should().Be(DateTimeOffset.Parse("2026-01-02T03:04:05Z"));
        result.CollectedAt.Should().Be(DateTimeOffset.Parse("2026-01-03T04:05:06Z"));
        result.CompletedAt.Should().Be(DateTimeOffset.Parse("2026-01-04T05:06:07Z"));
        result.CanceledAt.Should().Be(DateTimeOffset.Parse("2026-01-05T06:07:08Z"));

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("operationName").GetString().Should().Be("GetJ3Tracking");
        doc.RootElement.TryGetProperty("query", out _).Should().BeTrue();
        doc.RootElement.GetProperty("variables").GetProperty("trackingNumber").GetString()
            .Should().Be("TRK-123");
        body.Should().Contain("getTrackingOrderSeller");
    }

    [Fact]
    public async Task L_Tracking_NullCompletedAndCanceled_RemainNull()
    {
        var payload = """
            {
              "data": {
                "getTrackingOrderSeller": {
                  "id": "ord-2",
                  "code": "CODE-2",
                  "status": "CREATED",
                  "ecommerce": null,
                  "createdAt": "2026-02-01T00:00:00Z",
                  "collectedAt": null,
                  "completedAt": null,
                  "canceledAt": null
                }
              }
            }
            """;
        var client = CreateClient(_ => OkJson(payload));
        var result = await client.GetTrackingAsync("TRK-NULL");
        result.Should().NotBeNull();
        result!.CompletedAt.Should().BeNull();
        result.CanceledAt.Should().BeNull();
        result.CollectedAt.Should().BeNull();
        result.Ecommerce.Should().BeNull();
        result.Status.Should().Be("CREATED");
    }

    [Fact]
    public async Task M_Tracking_NotFound_ReturnsNull()
    {
        var client = CreateClient(_ => OkJson("""{"data":{"getTrackingOrderSeller":null}}"""));
        var result = await client.GetTrackingAsync("MISSING");
        result.Should().BeNull();
    }

    [Fact]
    public async Task N_Timeout_ThrowsSanitized_DoesNotHang()
    {
        var handler = new DelayedStubHandler(TimeSpan.FromSeconds(5));
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(200) };
        var client = new J3Client(
            http,
            Options.Create(CreateOptions()),
            NullLogger<J3Client>.Instance);

        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.Message.Should().Contain("timed out");
        ex.Which.Message.Should().NotContain(FakeToken);
    }

    [Fact]
    public async Task MissingUrl_ThrowsSanitized_WithoutStartupCrash()
    {
        var client = CreateClient(
            _ => OkJson("""{"data":{"isValidServiceArea":true}}"""),
            graphQlUrl: "");
        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.Message.Should().Contain("URL");
    }

    [Fact]
    public async Task MissingToken_ThrowsSanitized()
    {
        var client = CreateClient(
            _ => OkJson("""{"data":{"isValidServiceArea":true}}"""),
            token: "");
        var act = () => client.IsServiceAreaAsync("01310100");
        var ex = await act.Should().ThrowAsync<J3ApiException>();
        ex.Which.Message.Should().Contain("token");
    }

    [Fact]
    public async Task UserCancellation_PropagatesOperationCanceled()
    {
        var handler = new DelayedStubHandler(TimeSpan.FromSeconds(10));
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var client = new J3Client(
            http,
            Options.Create(CreateOptions()),
            NullLogger<J3Client>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var act = () => client.IsServiceAreaAsync("01310100", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static J3Client CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        string? companyGroupCode = "J3",
        string? graphQlUrl = TestGraphQlUrl,
        string? token = FakeToken)
    {
        var handler = new GuardedStubHandler(responder);
        var http = new HttpClient(handler);
        return new J3Client(
            http,
            Options.Create(CreateOptions(companyGroupCode, graphQlUrl, token)),
            NullLogger<J3Client>.Instance);
    }

    private static J3ShippingOptions CreateOptions(
        string? companyGroupCode = "J3",
        string? graphQlUrl = TestGraphQlUrl,
        string? token = FakeToken) =>
        new()
        {
            Enabled = false,
            GraphQlUrl = graphQlUrl,
            Token = token,
            CompanyGroupCode = companyGroupCode ?? "J3",
            TimeoutSeconds = 15
        };

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    /// <summary>Bloqueia qualquer request cujo host contenha o domínio de produção J3.</summary>
    private sealed class GuardedStubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public GuardedStubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri?.Host ?? string.Empty;
            if (host.Contains(ProductionHostMarker, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "J3Client tests must not call the production GraphQL host.");
            }

            return Task.FromResult(_responder(request));
        }
    }

    private sealed class DelayedStubHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayedStubHandler(TimeSpan delay) => _delay = delay;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri?.Host ?? string.Empty;
            if (host.Contains(ProductionHostMarker, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "J3Client tests must not call the production GraphQL host.");
            }

            await Task.Delay(_delay, cancellationToken);
            return OkJson("""{"data":{"isValidServiceArea":true}}""");
        }
    }
}
