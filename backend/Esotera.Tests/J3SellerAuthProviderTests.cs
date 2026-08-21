using System.Net;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

public class J3SellerAuthProviderTests
{
    private const string LoginUrl = "http://localhost/j3-auth-login/";
    private const string GraphQlUrl = "http://localhost/j3-graphql-test/";
    private const string ExpectedSellerId = "5d0baf45-51d7-4213-aba1-6a6079e2f496";
    private const string PasswordSecret = "super-secret-password-never-log";

    [Fact]
    public async Task Login200_ExtractsAccessToken()
    {
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(1));
        var auth = CreateProvider(
            (_, req) =>
            {
                if (IsLogin(req))
                    return LoginOk(token);
                return VerifyOk(ExpectedSellerId);
            });

        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().Be(token);
    }

    [Fact]
    public async Task PasswordAndToken_NeverAppearInLoggedMessages()
    {
        var logger = new CapturingLogger<J3SellerAuthProvider>();
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(1));
        var auth = CreateProvider(
            (_, req) => IsLogin(req) ? LoginOk(token) : VerifyOk(ExpectedSellerId),
            logger: logger,
            password: PasswordSecret);

        await auth.GetAccessTokenAsync();
        logger.Messages.Should().NotBeEmpty();
        foreach (var msg in logger.Messages)
        {
            msg.Should().NotContain(PasswordSecret);
            msg.Should().NotContain(token);
            msg.Should().NotContain("accessToken");
        }
    }

    [Fact]
    public async Task Cache_ReusesValidToken_SingleLogin()
    {
        var logins = 0;
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(2));
        var auth = CreateProvider((_, req) =>
        {
            if (IsLogin(req))
            {
                Interlocked.Increment(ref logins);
                return LoginOk(token);
            }

            return VerifyOk(ExpectedSellerId);
        });

        (await auth.GetAccessTokenAsync()).AccessToken.Should().Be(token);
        (await auth.GetAccessTokenAsync()).AccessToken.Should().Be(token);
        (await auth.GetAccessTokenAsync()).AccessToken.Should().Be(token);
        logins.Should().Be(1);
    }

    [Fact]
    public async Task ExpiringToken_TriggersNewLogin()
    {
        var logins = 0;
        var nearExp = MakeJwt(DateTimeOffset.UtcNow.AddMinutes(2)); // skew default 5 → renew now
        var auth = CreateProvider((_, req) =>
        {
            if (IsLogin(req))
            {
                Interlocked.Increment(ref logins);
                return LoginOk(nearExp);
            }

            return VerifyOk(ExpectedSellerId);
        }, renewSkewMinutes: 5);

        await auth.GetAccessTokenAsync();
        await auth.GetAccessTokenAsync();
        logins.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ConcurrentGets_OnlyOneLogin()
    {
        var logins = 0;
        var gate = new ManualResetEventSlim(false);
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(1));
        var auth = CreateProvider((_, req) =>
        {
            if (IsLogin(req))
            {
                gate.Wait(TimeSpan.FromSeconds(5));
                Interlocked.Increment(ref logins);
                Thread.Sleep(50);
                return LoginOk(token);
            }

            return VerifyOk(ExpectedSellerId);
        });

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                gate.Set();
                return await auth.GetAccessTokenAsync();
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(r => r.IsSuccess && r.AccessToken == token);
        logins.Should().Be(1);
    }

    [Fact]
    public async Task SellerIdMatch_Success()
    {
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(1));
        var auth = CreateProvider((_, req) =>
            IsLogin(req) ? LoginOk(token) : VerifyOk(ExpectedSellerId));
        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SellerIdMismatch_FailClosed()
    {
        var token = MakeJwt(DateTimeOffset.UtcNow.AddHours(1));
        var auth = CreateProvider((_, req) =>
            IsLogin(req) ? LoginOk(token) : VerifyOk("other-seller-id"));
        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.AuthSellerMismatch);
    }

    [Fact]
    public async Task Login401_SanitizedFailure()
    {
        var auth = CreateProvider((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"bad"}""")
            });
        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.AuthHttp401);
    }

    [Fact]
    public async Task InvalidJson_SafeFailure()
    {
        var auth = CreateProvider((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json{")
            });
        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.AuthJsonInvalid);
    }

    [Fact]
    public async Task MissingAccessToken_SafeFailure()
    {
        var auth = CreateProvider((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"user":{"id":"x"}}""")
            });
        var result = await auth.GetAccessTokenAsync();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.AuthTokenMissing);
    }

    [Fact]
    public async Task CreateTms_UsesSellerAuthToken_NotStaticToken()
    {
        string? authHeader = null;
        var sellerToken = "seller-bearer-from-auth";
        var auth = new FakeJ3SellerAuthProvider { NextToken = sellerToken };
        var client = CreateFulfillmentClient(req =>
        {
            authHeader = req.Headers.Authorization?.ToString();
            return OkCreateSuccess();
        }, auth, staticToken: "static-legacy-token-should-not-be-used", useLogin: true);

        var result = await client.CreateOrderAsync(
            ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.Success);
        authHeader.Should().Be($"Bearer {sellerToken}");
        auth.LoginSimulatedCount.Should().Be(1);
    }

    [Fact]
    public async Task Import_UsesSellerAuthToken()
    {
        string? authHeader = null;
        var sellerToken = "import-seller-token";
        var auth = new FakeJ3SellerAuthProvider { NextToken = sellerToken };
        var client = CreateImportClient(req =>
        {
            authHeader = req.Headers.Authorization?.ToString();
            return OkImportSuccess();
        }, auth, useLogin: true);

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.Success);
        authHeader.Should().Be($"Bearer {sellerToken}");
        auth.GetCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Mutation_ZeroAutomaticRetry_SinglePost()
    {
        var posts = 0;
        var auth = new FakeJ3SellerAuthProvider();
        var client = CreateFulfillmentClient(_ =>
        {
            posts++;
            return OkJson("""{"errors":[{"extensions":{"code":"UNAUTHENTICATED"}}]}""");
        }, auth, useLogin: true);

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        posts.Should().Be(1);
    }

    [Fact]
    public async Task Coverage_StillUsesStaticToken_UnaffectedBySellerAuth()
    {
        string? authHeader = null;
        var handler = new StubHandler(req =>
        {
            authHeader = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"isValidServiceArea":true}}""")
            };
        });
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var opts = new J3ShippingOptions
        {
            Enabled = true,
            GraphQlUrl = GraphQlUrl,
            Token = "coverage-static-token",
            CompanyGroupCode = "J3",
            LoginEmail = "seller@example.com",
            LoginPassword = PasswordSecret,
            SellerId = ExpectedSellerId
        };
        var client = new J3Client(http, Options.Create(opts), NullLogger<J3Client>.Instance);
        var ok = await client.IsServiceAreaAsync("03065000");
        ok.Should().BeTrue();
        authHeader.Should().Be("Bearer coverage-static-token");
    }

    [Fact]
    public async Task Flags_StillRespected_ImportDisabled_ZeroAuthCall()
    {
        var auth = new FakeJ3SellerAuthProvider();
        var posts = 0;
        var client = CreateImportClient(_ =>
        {
            posts++;
            return OkImportSuccess();
        }, auth, useLogin: true, importEnabled: false);

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.ImportByAccessKeyDisabled);
        posts.Should().Be(0);
        auth.GetCallCount.Should().Be(0);
    }

    [Fact]
    public void JwtExpReader_ReadsExp_WithoutValidatingSignature()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(3);
        var jwt = MakeJwt(exp);
        var read = J3JwtExpReader.TryReadExpiresAtUtc(jwt);
        read.Should().NotBeNull();
        Math.Abs((read!.Value - exp).TotalSeconds).Should().BeLessThan(2);
    }

    private static J3SellerAuthProvider CreateProvider(
        Func<HttpRequestMessage, HttpRequestMessage, HttpResponseMessage> responder,
        ILogger<J3SellerAuthProvider>? logger = null,
        string password = PasswordSecret,
        int renewSkewMinutes = 5)
    {
        var handler = new StubHandler(req => responder(req, req));
        var factory = new SimpleHttpClientFactory(handler);
        var opts = new J3ShippingOptions
        {
            LoginEmail = "seller@example.com",
            LoginPassword = password,
            LoginUrl = LoginUrl,
            GraphQlUrl = GraphQlUrl,
            SellerId = ExpectedSellerId,
            CompanyGroupCode = "J3",
            AuthRenewSkewMinutes = renewSkewMinutes,
            TimeoutSeconds = 15
        };
        return new J3SellerAuthProvider(
            factory,
            Options.Create(opts),
            logger ?? NullLogger<J3SellerAuthProvider>.Instance);
    }

    private static J3FulfillmentHttpClient CreateFulfillmentClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        IJ3SellerAuthProvider auth,
        string staticToken = "legacy-token",
        bool useLogin = false)
    {
        var http = new HttpClient(new StubHandler(responder)) { Timeout = TimeSpan.FromSeconds(15) };
        var opts = new J3ShippingOptions
        {
            FulfillmentEnabled = true,
            GraphQlUrl = GraphQlUrl,
            Token = staticToken,
            SellerId = ExpectedSellerId,
            SellerInformationId = "seller-info",
            CompanyGroupCode = "J3",
            LoginEmail = useLogin ? "seller@example.com" : null,
            LoginPassword = useLogin ? PasswordSecret : null
        };
        return new J3FulfillmentHttpClient(
            http, Options.Create(opts), auth, NullLogger<J3FulfillmentHttpClient>.Instance);
    }

    private static J3ImportOrderByAccessKeyHttpClient CreateImportClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        IJ3SellerAuthProvider auth,
        bool useLogin = false,
        bool importEnabled = true)
    {
        var http = new HttpClient(new StubHandler(responder)) { Timeout = TimeSpan.FromSeconds(15) };
        var opts = new J3ShippingOptions
        {
            ImportByAccessKeyEnabled = importEnabled,
            FulfillmentEnabled = false,
            GraphQlUrl = GraphQlUrl,
            Token = "legacy-token",
            SellerId = ExpectedSellerId,
            SellerInformationId = "seller-info",
            EmitterPhone = "1122973518",
            CompanyGroupCode = "J3",
            LoginEmail = useLogin ? "seller@example.com" : null,
            LoginPassword = useLogin ? PasswordSecret : null
        };
        return new J3ImportOrderByAccessKeyHttpClient(
            http, Options.Create(opts), auth, NullLogger<J3ImportOrderByAccessKeyHttpClient>.Instance);
    }

    private static bool IsLogin(HttpRequestMessage req) =>
        req.RequestUri!.AbsoluteUri.Contains("j3-auth-login", StringComparison.OrdinalIgnoreCase);

    private static HttpResponseMessage LoginOk(string token) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { accessToken = token, user = new { id = "u1" } }),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage VerifyOk(string sellerId) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"mySellerMetadata\":{\"sellerId\":\"" + sellerId + "\"}}}",
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage OkCreateSuccess() =>
        OkJson("""{"data":{"createTmsOrders":[{"success":true,"orderId":"j3-1","message":"ok","index":0}]}}""");

    private static HttpResponseMessage OkImportSuccess() =>
        OkJson("""{"data":{"importOrderByAccessKey":{"success":true,"message":"ok","error":null}}}""");

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string MakeJwt(DateTimeOffset expiresAt)
    {
        static string B64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64Url(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payloadJson = "{\"exp\":" + expiresAt.ToUnixTimeSeconds() + ",\"sub\":\"test\"}";
        var payload = B64Url(Encoding.UTF8.GetBytes(payloadJson));
        return $"{header}.{payload}.sig";
    }

    private static Order ValidOrder() => new()
    {
        Id = Guid.NewGuid(),
        CustomerName = "Maria Silva",
        CustomerPhone = "11988887777",
        ShipCep = "03065000",
        ShipStreet = "Rua A",
        ShipNumber = "10",
        ShipComplement = "Apto 1",
        ShipNeighborhood = "Bairro",
        ShipCity = "São Paulo",
        ShipState = "SP",
        ShippingIsResidentialAddress = true,
        Subtotal = 100m,
        Discount = 10m,
        ShippingPrice = 20m
    };

    private static StoreSettings ValidSettings() => new()
    {
        PackageWeightGrams = 400,
        PackageLengthCm = 16,
        PackageWidthCm = 11,
        PackageHeightCm = 6
    };

    private static J3FiscalEligibilitySnapshot ValidFiscal() => new()
    {
        Status = FiscalInvoiceStatus.Authorized,
        ChNFe = new string('9', 44),
        Number = "3",
        Series = "9",
        AuthorizedAtUtc = DateTime.UtcNow
    };

    private static FiscalInvoiceParseResult ValidParsed()
    {
        var parser = new FiscalInvoiceXmlParser();
        return parser.Parse(Encoding.UTF8.GetBytes(FiscalInvoiceImportTests.BuildSyntheticAuthorizedXml()));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SimpleHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
