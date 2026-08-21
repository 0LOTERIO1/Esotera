using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// createTmsOrders via HttpMessageHandler fake. Zero rede real. Host j3tms.com.br bloqueado.
/// </summary>
public class J3CreateTmsOrderClientTests
{
    private const string FakeToken = "fake-j3-token-for-tests";
    private const string TestGraphQlUrl = "http://localhost/j3-graphql-test/";
    private const string ProductionHostMarker = "j3tms.com.br";
    private const string SellerId = "seller-test";
    private const string SellerInformationId = "seller-info-test";

    [Fact]
    public void Mutation_CreateJ3TmsOrders_SelectsApiErrorFields()
    {
        J3CreateTmsOrderMutation.OperationName.Should().Be("CreateJ3TmsOrders");
        J3CreateTmsOrderMutation.Document.Should().Contain("mutation CreateJ3TmsOrders");
        J3CreateTmsOrderMutation.Document.Should().Contain("createTmsOrders");
        J3CreateTmsOrderMutation.Document.Should().Contain("$inputs: [CreateTmsOrderInput!]!");
        J3CreateTmsOrderMutation.Document.Should().NotContain("createTmsOrder(");
        J3CreateTmsOrderMutation.Document.Should().Contain("layer");
        J3CreateTmsOrderMutation.Document.Should().Contain("clientId");
        J3CreateTmsOrderMutation.Document.Should().Contain("errorCode");
        J3CreateTmsOrderMutation.Document.Should().Contain("description");
        J3CreateTmsOrderMutation.Document.Should().Contain("errorField");
        J3CreateTmsOrderMutation.Document.Should().Contain("index");
    }

    [Fact]
    public void Ij3Client_RemainsReadOnly_MutationLivesOnFulfillmentClient()
    {
        typeof(IJ3Client).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Should().BeEquivalentTo("IsServiceAreaAsync", "GetTrackingAsync");

        typeof(IJ3FulfillmentClient).GetMethod(nameof(IJ3FulfillmentClient.CreateOrderAsync))
            .Should().NotBeNull();
        typeof(IJ3FulfillmentClient).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Should().NotContain(n => n.Contains("Stamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FulfillmentService_AndPaymentService_DoNotTakeMutationClient()
    {
        typeof(J3FulfillmentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IJ3FulfillmentClient));

        typeof(PaymentService).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain(typeof(IJ3FulfillmentClient));
    }

    [Fact]
    public async Task FulfillmentFlagFalse_ZeroHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        }, opts => opts.FulfillmentEnabled = false);

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.FulfillmentDisabled);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task J3EnabledFalse_FulfillmentTrue_StillSendsHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        }, opts => opts.Enabled = false);

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.Success);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task SellerIdMissing_ZeroHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        }, opts => opts.SellerId = "  ");

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.MissingSellerId);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task SellerInformationIdMissing_ZeroHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        }, opts => opts.SellerInformationId = "");

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.MissingSellerInformationId);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task ResidentialNull_ZeroHttp_DefiniteFailure()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var result = await client.CreateOrderAsync(ValidOrder(residential: null), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.ResidentialRequired);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task InvalidCep_ZeroHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var order = ValidOrder();
        order.ShipCep = "123";
        var result = await client.CreateOrderAsync(order, ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.InvalidCep);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task FiscalNull_ZeroHttp_LocalFailure()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), fiscal: null);
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be("MISSING_FISCAL_INVOICE");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task FiscalNotAuthorized_ZeroHttp_LocalFailure()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var fiscal = ValidFiscal() with { Status = FiscalInvoiceStatus.Unknown };
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), fiscal);
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be("FISCAL_NOT_AUTHORIZED");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task FiscalInvalidChNFe_ZeroHttp_LocalFailure()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var fiscal = ValidFiscal() with { ChNFe = "123" };
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), fiscal);
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be("INVALID_NFE_KEY");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task FiscalMissingChNFe_ZeroHttp_LocalFailure()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        });

        var fiscal = ValidFiscal() with { ChNFe = null };
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), fiscal);
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be("MISSING_NFE_KEY");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Payload_CreateTmsOrders_SingleInput_SellerIdInside_NoEcommerceOrPackages()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var order = ValidOrder();
        order.ShipCep = "03065000";
        order.ShipNeighborhood = "Belenzinho";
        await client.CreateOrderAsync(order, ValidSettings(), ValidFiscal());

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("operationName").GetString().Should().Be("CreateJ3TmsOrders");
        var query = doc.RootElement.GetProperty("query").GetString();
        query.Should().Contain("createTmsOrders");
        query.Should().Contain("layer");
        query.Should().Contain("clientId");
        query.Should().Contain("errorCode");
        query.Should().Contain("description");

        var variables = doc.RootElement.GetProperty("variables");
        variables.TryGetProperty("sellerId", out _).Should().BeFalse();
        variables.TryGetProperty("input", out _).Should().BeFalse();

        var inputs = variables.GetProperty("inputs");
        inputs.ValueKind.Should().Be(JsonValueKind.Array);
        inputs.GetArrayLength().Should().Be(1);

        var input = inputs[0];
        input.GetProperty("sellerId").GetString().Should().Be(SellerId);
        input.GetProperty("sellerInformationId").GetString().Should().Be(SellerInformationId);
        input.GetProperty("orderPickupType").GetString().Should().Be("Standard");
        input.GetProperty("quantity").GetInt32().Should().Be(1);
        input.GetProperty("totalPackageValueInCents").GetInt32().Should().Be(9000);
        input.TryGetProperty("ecommerce", out _).Should().BeFalse();
        input.TryGetProperty("packages", out _).Should().BeFalse();
        input.GetProperty("nf").GetString().Should().Be("2");
        input.GetProperty("nfKey").GetString().Should().Be(new string('9', 44));
        input.GetProperty("nfSeries").GetString().Should().Be("9");
        input.TryGetProperty("danfe", out _).Should().BeFalse();

        var dp = input.GetProperty("deliveryPoint");
        dp.GetProperty("addressDistric").GetString().Should().Be("Belenzinho");
        dp.GetProperty("addressZipCode").GetString().Should().Be("03065-000");
        dp.GetProperty("isResidentialAddress").GetBoolean().Should().BeTrue();
        dp.GetProperty("addressStreet").GetString().Should().Be("Rua A");
        dp.GetProperty("addressNumber").GetString().Should().Be("10");
        dp.GetProperty("contactName").GetString().Should().Be("Maria Silva");
    }

    [Fact]
    public async Task Payload_ResidentialFalse_Preserved()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        await client.CreateOrderAsync(ValidOrder(residential: false), ValidSettings(), ValidFiscal());

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("variables").GetProperty("inputs")[0]
            .GetProperty("deliveryPoint").GetProperty("isResidentialAddress").GetBoolean()
            .Should().BeFalse();
    }

    [Fact]
    public async Task Payload_WithAuthorizedFiscal_IncludesNfNfKeyNfSeries_OmitsDanfe()
    {
        var chNFe = new string('8', 44);

        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var fiscal = new J3FiscalEligibilitySnapshot
        {
            Status = FiscalInvoiceStatus.Authorized,
            ChNFe = $"  {chNFe}  ",
            Number = "2",
            Series = "9",
            AuthorizedAtUtc = DateTime.UtcNow
        };

        await client.CreateOrderAsync(ValidOrder(), ValidSettings(), fiscal);

        using var doc = JsonDocument.Parse(body!);
        var input = doc.RootElement.GetProperty("variables").GetProperty("inputs")[0];
        input.GetProperty("nf").GetString().Should().Be("2");
        input.GetProperty("nfKey").GetString().Should().Be(chNFe);
        input.GetProperty("nfKey").GetString()!.Length.Should().Be(44);
        input.GetProperty("nfSeries").GetString().Should().Be("9");
        input.TryGetProperty("danfe", out _).Should().BeFalse();
        input.TryGetProperty("packages", out _).Should().BeFalse();
        input.TryGetProperty("ecommerce", out _).Should().BeFalse();
        body.Should().NotContain("XmlCipher");
    }

    [Fact]
    public async Task Payload_PhoneOptional_OmittedWhenEmpty()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var order = ValidOrder();
        order.CustomerPhone = "  ";
        await client.CreateOrderAsync(order, ValidSettings(), ValidFiscal());

        using var doc = JsonDocument.Parse(body!);
        var dp = doc.RootElement.GetProperty("variables").GetProperty("inputs")[0]
            .GetProperty("deliveryPoint");
        dp.TryGetProperty("contactPhoneNumber", out _).Should().BeFalse();
    }

    [Fact]
    public void Mapper_WithFiscal_MapsNfFields_WithoutDanfeProperty()
    {
        var built = J3CreateTmsOrderMapper.TryBuild(
            ValidOrder(),
            ValidSettings(),
            EnabledOptions(),
            ValidFiscal());

        built.IsValid.Should().BeTrue();
        built.Command!.Input.Nf.Should().Be("2");
        built.Command.Input.NfKey.Should().Be(new string('9', 44));
        built.Command.Input.NfSeries.Should().Be("9");
        typeof(J3CreateTmsOrderInputDto).GetProperty("Danfe").Should().BeNull();
    }

    [Fact]
    public async Task Payload_MerchandiseWithoutShipping_NoPackages()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var order = ValidOrder();
        order.Subtotal = 100m;
        order.Discount = 10m;
        order.ShippingPrice = 50m;
        await client.CreateOrderAsync(order, ValidSettings(), ValidFiscal());

        using var doc = JsonDocument.Parse(body!);
        var input = doc.RootElement.GetProperty("variables").GetProperty("inputs")[0];
        input.GetProperty("totalPackageValueInCents").GetInt32().Should().Be(9000);
        input.TryGetProperty("packages", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AlreadyMaskedCep_StaysMasked()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var order = ValidOrder();
        order.ShipCep = "03065-000";
        await client.CreateOrderAsync(order, ValidSettings(), ValidFiscal());

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("variables").GetProperty("inputs")[0]
            .GetProperty("deliveryPoint").GetProperty("addressZipCode").GetString()
            .Should().Be("03065-000");
    }

    [Fact]
    public async Task SuccessTrue_WithOrderId_IsSuccess()
    {
        var client = CreateClient(_ => SuccessJson("j3-order-1"));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.Success);
        result.OrderId.Should().Be("j3-order-1");
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task SuccessTrue_EmptyOrderId_IsUnknownOutcome()
    {
        var client = CreateClient(_ => SuccessJson(orderId: ""));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.SuccessWithoutOrderId);
    }

    [Fact]
    public async Task SuccessFalse_IsUnknownOutcome_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson(
                """{"data":{"createTmsOrders":[{"success":false,"message":"rejected","orderId":null,"index":0}]}}""");
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.SuccessFalse);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task EmptyResultArray_IsUnknownOutcome()
    {
        var client = CreateClient(_ => OkJson("""{"data":{"createTmsOrders":[]}}"""));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.UnexpectedResultCount);
    }

    [Fact]
    public async Task ResultArrayGreaterThanOne_IsUnknownOutcome()
    {
        var client = CreateClient(_ => OkJson(
            """{"data":{"createTmsOrders":[{"success":true,"orderId":"a","index":0},{"success":true,"orderId":"b","index":1}]}}"""));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.UnexpectedResultCount);
    }

    [Fact]
    public async Task UnexpectedIndex_IsUnknownOutcome()
    {
        var client = CreateClient(_ => OkJson(
            """{"data":{"createTmsOrders":[{"success":true,"orderId":"j3-order-1","index":1}]}}"""));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.UnexpectedIndex);
    }

    [Fact]
    public async Task TimeoutAfterSendAsync_IsUnknownOutcome_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            throw new TaskCanceledException("simulated timeout after send");
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.TimeoutUnknown);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Http500_IsUnknownOutcome_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.Http500);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task TruncatedJson_IsUnknownOutcome()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":{"createTmsOrders":[{"success":tru""", Encoding.UTF8, "application/json")
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.JsonInvalid);
    }

    [Fact]
    public async Task AmbiguousGraphqlError_IsUnknownOutcome()
    {
        var client = CreateClient(_ => OkJson("""{"errors":[{"message":"internal"}]}"""));
        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlAmbiguous);
    }

    [Fact]
    public async Task GraphqlUnauthenticated_IsDefiniteFailure_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson("""{"errors":[{"message":"nope","extensions":{"code":"UNAUTHENTICATED"}}]}""");
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlUnauthenticated);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GraphqlValidationFailed_IsDefiniteFailure_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson("""{"errors":[{"extensions":{"code":"GRAPHQL_VALIDATION_FAILED"}}]}""");
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlValidation);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GraphqlParseFailed_Http400_IsDefiniteFailure_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"errors":[{"extensions":{"code":"GRAPHQL_PARSE_FAILED"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlValidation);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Http400_WithoutPreExecutionProof_IsUnknown()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("nope", Encoding.UTF8, "text/plain")
            };
        });

        var result = await client.CreateOrderAsync(ValidOrder(), ValidSettings(), ValidFiscal());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        calls.Should().Be(1);
    }

    [Fact]
    public void Mapper_DoesNotDefaultResidential()
    {
        var opts = EnabledOptions();
        J3CreateTmsOrderMapper.TryBuild(ValidOrder(residential: null), ValidSettings(), opts)
            .IsValid.Should().BeFalse();

        var builtTrue = J3CreateTmsOrderMapper.TryBuild(ValidOrder(residential: true), ValidSettings(), opts);
        builtTrue.Command!.Input.DeliveryPoint.IsResidentialAddress.Should().BeTrue();
        builtTrue.Command.Input.SellerId.Should().Be(SellerId);

        var builtFalse = J3CreateTmsOrderMapper.TryBuild(ValidOrder(residential: false), ValidSettings(), opts);
        builtFalse.Command!.Input.DeliveryPoint.IsResidentialAddress.Should().BeFalse();
    }

    private static J3FiscalEligibilitySnapshot ValidFiscal() => new()
    {
        Status = FiscalInvoiceStatus.Authorized,
        ChNFe = new string('9', 44),
        Number = "2",
        Series = "9",
        AuthorizedAtUtc = DateTime.UtcNow
    };

    private static J3FulfillmentHttpClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        Action<J3ShippingOptions>? configure = null)
    {
        var handler = new GuardedStubHandler(responder);
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var opts = EnabledOptions();
        configure?.Invoke(opts);
        return new J3FulfillmentHttpClient(http, Options.Create(opts), NullLogger<J3FulfillmentHttpClient>.Instance);
    }

    private static J3ShippingOptions EnabledOptions() => new()
    {
        Enabled = true,
        FulfillmentEnabled = true,
        GraphQlUrl = TestGraphQlUrl,
        Token = FakeToken,
        CompanyGroupCode = "J3",
        SellerId = SellerId,
        SellerInformationId = SellerInformationId,
        Ecommerce = "Standalone",
        OrderPickupType = "Standard",
        TimeoutSeconds = 15
    };

    private static Order ValidOrder(bool? residential = true) => new()
    {
        Id = Guid.NewGuid(),
        ShipCep = "03065000",
        ShipStreet = "Rua A",
        ShipNumber = "10",
        ShipComplement = "Apto 1",
        ShipNeighborhood = "Bairro",
        ShipCity = "São Paulo",
        ShipState = "SP",
        ShippingIsResidentialAddress = residential,
        CustomerName = "Maria Silva",
        CustomerPhone = "11988887777",
        Subtotal = 100m,
        Discount = 10m,
        ShippingPrice = 20m
    };

    private static StoreSettings ValidSettings() => new()
    {
        PackageLengthCm = 16m,
        PackageWidthCm = 11m,
        PackageHeightCm = 6m,
        PackageWeightGrams = 400
    };

    private static HttpResponseMessage SuccessJson(string orderId = "j3-order-1") =>
        OkJson(
            "{\"data\":{\"createTmsOrders\":[{\"success\":true,\"message\":\"ok\",\"orderId\":"
            + JsonSerializer.Serialize(orderId)
            + ",\"index\":0}]}}");

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

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
                    "J3 fulfillment tests must not call the production GraphQL host.");
            }

            return Task.FromResult(_responder(request));
        }
    }
}
