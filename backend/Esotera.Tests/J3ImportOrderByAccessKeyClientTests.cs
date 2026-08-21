using System.Net;
using System.Text;
using System.Text.Json;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

/// <summary>
/// importOrderByAccessKey via HttpMessageHandler fake. Zero rede real / zero mutation Production.
/// </summary>
public class J3ImportOrderByAccessKeyClientTests
{
    private const string FakeToken = "fake-j3-token-for-tests";
    private const string TestGraphQlUrl = "http://localhost/j3-graphql-test/";
    private const string ProductionHostMarker = "j3tms.com.br";
    private const string SellerId = "seller-test";
    private const string SellerInformationId = "seller-info-test";

    [Fact]
    public void Mutation_IncludesOperationName_AndImportOrderByAccessKey()
    {
        J3ImportOrderByAccessKeyMutation.OperationName.Should().Be("ImportJ3OrderByAccessKey");
        J3ImportOrderByAccessKeyMutation.Document.Should().Contain("importOrderByAccessKey");
        J3ImportOrderByAccessKeyMutation.Document.Should().Contain("$input: ImportOrderByAccessKeyInput!");
        J3ImportOrderByAccessKeyMutation.Document.Should().NotContain("createTmsOrders");
    }

    [Fact]
    public void Clients_AreSeparate_NoSilentFallback()
    {
        typeof(IJ3FulfillmentClient).GetMethods()
            .Select(m => m.Name)
            .Should().NotContain("ImportAsync");
        typeof(IJ3ImportOrderByAccessKeyClient).GetMethods()
            .Select(m => m.Name)
            .Should().BeEquivalentTo("ImportAsync");
    }

    [Fact]
    public async Task Envelope_IncludesOperationName_AndNfePayload()
    {
        string? body = null;
        var client = CreateClient(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SuccessJson();
        });

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.Success);

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("operationName").GetString().Should().Be("ImportJ3OrderByAccessKey");
        doc.RootElement.TryGetProperty("query", out _).Should().BeTrue();
        var input = doc.RootElement.GetProperty("variables").GetProperty("input");
        input.GetProperty("sellerInformationId").GetString().Should().Be(SellerInformationId);
        input.GetProperty("sellerId").GetString().Should().Be(SellerId);
        var order = input.GetProperty("order");
        order.GetProperty("chNFe").GetString().Should().Be(FiscalInvoiceImportTests.SyntheticChNFe);
        order.GetProperty("destXNome").GetString().Should().Be("Destinatario Fixture Sintetico");
        order.GetProperty("emitXNome").GetString().Should().Be("Emitente Fixture Sintetico LTDA");
        order.GetProperty("emitXFant").GetString().Should().Be("Emitente Fantasia");
        var dest = order.GetProperty("destEnder");
        dest.GetProperty("CEP").GetString().Should().Be("03065-000");
        dest.GetProperty("fone").GetString().Should().Be("11988887777");
        dest.GetProperty("nro").GetString().Should().Be("200");
        dest.GetProperty("xLgr").GetString().Should().Be("Rua Destinatario");
        var emit = order.GetProperty("emitEnder");
        emit.GetProperty("CEP").GetString().Should().Be("01310-100");
        emit.GetProperty("fone").GetString().Should().Be("1133334444");
        var items = order.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("qCom").GetInt32().Should().Be(1);
        items[0].GetProperty("vUnCom").GetDouble().Should().Be(54.9);
        items[0].GetProperty("xProd").GetString().Should().Be("Produto Fixture Sintetico");
        body.Should().NotContain("XmlCipher");
        body.Should().NotContain("<nfeProc");
    }

    [Fact]
    public void Mapper_RequiresEmitPhone_AndWholeQuantity()
    {
        var parsed = CloneParsed(ValidParsed(), emitPhone: null);
        var opts = EnabledOptions();
        opts.EmitterPhone = null;
        J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), parsed, opts)
            .IsValid.Should().BeFalse();

        var fractional = CloneParsed(ValidParsed(), qty: 1.5m);
        J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), fractional, EnabledOptions())
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Mapper_EmitPhone_PrefersXmlOverEmitterPhoneConfig()
    {
        var parsed = ValidParsed();
        parsed.IssuerAddress!.PhoneDigits.Should().Be("1133334444");
        var opts = EnabledOptions();
        opts.EmitterPhone = "(11) 2297-3518";
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), parsed, opts);
        built.IsValid.Should().BeTrue();
        built.Command!.Input.Order.EmitEnder.Fone.Should().Be("1133334444");
    }

    [Fact]
    public void Mapper_EmitPhone_UsesEmitterPhoneWhenXmlMissing()
    {
        var parsed = CloneParsed(ValidParsed(), emitPhone: null);
        var opts = EnabledOptions();
        opts.EmitterPhone = "(11) 2297-3518";
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), parsed, opts);
        built.IsValid.Should().BeTrue();
        built.Command!.Input.Order.EmitEnder.Fone.Should().Be("1122973518");
    }

    [Fact]
    public void Mapper_EmitPhone_FailClosedWhenXmlAndConfigMissing()
    {
        var parsed = CloneParsed(ValidParsed(), emitPhone: null);
        var opts = EnabledOptions();
        opts.EmitterPhone = "  ";
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), parsed, opts);
        built.IsValid.Should().BeFalse();
        built.ErrorCode.Should().Be("MISSING_EMIT_ENDER");
    }

    [Fact]
    public void Mapper_DestPhone_NeverBecomesEmitPhone()
    {
        var parsed = CloneParsed(ValidParsed(), emitPhone: null, destPhone: null);
        var order = ValidOrder();
        order.CustomerPhone = "11977776666";
        var opts = EnabledOptions();
        opts.EmitterPhone = null;
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(order, parsed, opts);
        built.IsValid.Should().BeFalse();
        built.ErrorCode.Should().Be("MISSING_EMIT_ENDER");
    }

    [Fact]
    public void Mapper_UsesEmitXNome_WhenXFantMissing()
    {
        var parsed = CloneParsed(ValidParsed(), emitFant: null);
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(ValidOrder(), parsed, EnabledOptions());
        built.IsValid.Should().BeTrue();
        built.Command!.Input.Order.EmitXFant.Should().Be("Emitente Fixture Sintetico LTDA");
    }

    [Fact]
    public void Mapper_DestPhone_CanFallbackToOrderPhone()
    {
        var parsed = CloneParsed(ValidParsed(), destPhone: null);
        var order = ValidOrder();
        order.CustomerPhone = "11977776666";
        var built = J3ImportOrderByAccessKeyMapper.TryBuild(order, parsed, EnabledOptions());
        built.IsValid.Should().BeTrue();
        built.Command!.Input.Order.DestEnder.Fone.Should().Be("11977776666");
        built.Command.Input.Order.EmitEnder.Fone.Should().NotBe("11977776666");
    }

    [Fact]
    public async Task GraphqlUnauthenticated_IsDefiniteFailure_ZeroRetry()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson("""{"errors":[{"message":"not auth","extensions":{"code":"UNAUTHENTICATED"}}]}""");
        });

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlUnauthenticated);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GraphqlValidation_IsDefiniteFailure_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson("""{"errors":[{"extensions":{"code":"GRAPHQL_VALIDATION_FAILED"}}]}""");
        });

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.GraphqlValidation);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task AmbiguousGraphqlError_IsUnknownOutcome_SinglePost()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return OkJson("""{"errors":[{"message":"resolver boom","extensions":{"code":"INTERNAL_SERVER_ERROR"}}]}""");
        });

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.UnknownOutcome);
        result.ErrorCode.Should().Be("INTERNAL_SERVER_ERROR");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task FlagDisabled_ZeroHttp()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            return SuccessJson();
        }, opts => opts.ImportByAccessKeyEnabled = false);

        var result = await client.ImportAsync(ValidOrder(), ValidParsed());
        result.Outcome.Should().Be(J3CreateOrderOutcome.DefiniteFailure);
        result.ErrorCode.Should().Be(J3FulfillmentErrorCodes.ImportByAccessKeyDisabled);
        calls.Should().Be(0);
    }

    [Fact]
    public void Parser_ExtractsEmitDestAddresses_AndXProd()
    {
        var parser = new FiscalInvoiceXmlParser();
        var result = parser.Parse(Encoding.UTF8.GetBytes(FiscalInvoiceImportTests.BuildSyntheticAuthorizedXml()));
        result.IssuerName.Should().Be("Emitente Fixture Sintetico LTDA");
        result.IssuerTradeName.Should().Be("Emitente Fantasia");
        result.IssuerAddress!.ZipCodeDigits.Should().Be("01310100");
        result.IssuerAddress.PhoneDigits.Should().Be("1133334444");
        result.RecipientAddress!.ZipCodeDigits.Should().Be("03065000");
        result.Items[0].ProductName.Should().Be("Produto Fixture Sintetico");
    }

    private static J3ImportOrderByAccessKeyHttpClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        Action<J3ShippingOptions>? configure = null)
    {
        var handler = new GuardedStubHandler(responder);
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        var opts = EnabledOptions();
        configure?.Invoke(opts);
        return new J3ImportOrderByAccessKeyHttpClient(
            http,
            Options.Create(opts),
            new FakeJ3SellerAuthProvider(),
            NullLogger<J3ImportOrderByAccessKeyHttpClient>.Instance);
    }

    private static J3ShippingOptions EnabledOptions() => new()
    {
        Enabled = true,
        FulfillmentEnabled = false,
        ImportByAccessKeyEnabled = true,
        GraphQlUrl = TestGraphQlUrl,
        Token = FakeToken,
        CompanyGroupCode = "J3",
        SellerId = SellerId,
        SellerInformationId = SellerInformationId,
        EmitterPhone = "1122973518",
        TimeoutSeconds = 15
    };

    private static Order ValidOrder() => new()
    {
        Id = Guid.NewGuid(),
        CustomerName = "Maria Silva",
        CustomerPhone = "11988887777",
        ShipCep = "03065000",
        ShipStreet = "Rua A",
        ShipNumber = "10",
        ShipNeighborhood = "Bairro",
        ShipCity = "São Paulo",
        ShipState = "SP",
        ShippingIsResidentialAddress = true,
        Subtotal = 54.9m,
        Discount = 0m
    };

    private static FiscalInvoiceParseResult ValidParsed()
    {
        var parser = new FiscalInvoiceXmlParser();
        return parser.Parse(Encoding.UTF8.GetBytes(FiscalInvoiceImportTests.BuildSyntheticAuthorizedXml()));
    }

    private static FiscalInvoiceParseResult CloneParsed(
        FiscalInvoiceParseResult src,
        string? emitPhone = "keep",
        string? destPhone = "keep",
        string? emitFant = "keep",
        decimal? qty = null)
    {
        var emitAddr = src.IssuerAddress is null
            ? null
            : new FiscalNfeAddressSnapshot
            {
                Street = src.IssuerAddress.Street,
                Number = src.IssuerAddress.Number,
                Complement = src.IssuerAddress.Complement,
                ZipCodeDigits = src.IssuerAddress.ZipCodeDigits,
                PhoneDigits = emitPhone == "keep" ? src.IssuerAddress.PhoneDigits : emitPhone
            };
        var destAddr = src.RecipientAddress is null
            ? null
            : new FiscalNfeAddressSnapshot
            {
                Street = src.RecipientAddress.Street,
                Number = src.RecipientAddress.Number,
                Complement = src.RecipientAddress.Complement,
                ZipCodeDigits = src.RecipientAddress.ZipCodeDigits,
                PhoneDigits = destPhone == "keep" ? src.RecipientAddress.PhoneDigits : destPhone
            };

        var items = src.Items.Select(i => i with
        {
            Quantity = qty ?? i.Quantity
        }).ToList();

        return new FiscalInvoiceParseResult
        {
            ChNFe = src.ChNFe,
            Number = src.Number,
            Series = src.Series,
            HasAuthorizationEvidence = src.HasAuthorizationEvidence,
            IssuerName = src.IssuerName,
            IssuerTradeName = emitFant == "keep" ? src.IssuerTradeName : emitFant,
            IssuerAddress = emitAddr,
            RecipientName = src.RecipientName,
            RecipientAddress = destAddr,
            Items = items
        };
    }

    private static HttpResponseMessage SuccessJson() =>
        OkJson("""{"data":{"importOrderByAccessKey":{"success":true,"message":"ok","error":null}}}""");

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
                    "J3 import tests must not call the production GraphQL host.");
            }

            return Task.FromResult(_responder(request));
        }
    }
}
