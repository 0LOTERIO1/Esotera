using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Esotera.Application.Common;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// Fixture nfeProc 100% sintética (estrutura portal fiscal). Sem PII/chaves/protocolos reais.
/// </summary>
public class FiscalInvoiceImportTests : IClassFixture<CustomWebApplicationFactory>
{
    public const string SyntheticChNFe = "35260820999999999999999999999999999999999999";
    public const string SyntheticCpf = "52998224725";
    public const string SyntheticSku = "SKU-WAITE-TAROT";
    public const string SyntheticCProdUpsellerStyle = "SKUWAITETAROT";
    public const string SyntheticIssuerCnpj = "11222333000181";
    public const string SyntheticXPed = "UPAYHF999999";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FiscalInvoiceImportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Fixture sanitizada no layout nfeProc 4.00 — valores fictícios; sem Signature/X509.
    /// </summary>
    public static string BuildSyntheticAuthorizedXml(
        string chNFe = SyntheticChNFe,
        string cpf = SyntheticCpf,
        string cProd = SyntheticCProdUpsellerStyle,
        decimal qty = 1m,
        decimal total = 54.90m,
        string number = "123",
        string series = "8",
        string xPed = SyntheticXPed,
        string cStat = "100",
        string xMotivo = "Autorizado o uso da NF-e",
        bool includeProtocol = true,
        string? chNFeInId = null)
    {
        var idCh = chNFeInId ?? chNFe;
        var inv = CultureInfo.InvariantCulture;
        var totalText = total.ToString(inv);
        var qtyText = qty.ToString(inv);
        var unitText = total.ToString(inv);

        var protocolXml = includeProtocol
            ? $"""
                  <protNFe versao="4.00">
                    <infProt>
                      <tpAmb>1</tpAmb>
                      <chNFe>{chNFe}</chNFe>
                      <dhRecbto>2026-08-20T15:01:00-03:00</dhRecbto>
                      <nProt>999999999999999</nProt>
                      <cStat>{cStat}</cStat>
                      <xMotivo>{xMotivo}</xMotivo>
                    </infProt>
                  </protNFe>
                """
            : string.Empty;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe{idCh}" versao="4.00">
                  <ide>
                    <mod>55</mod>
                    <serie>{series}</serie>
                    <nNF>{number}</nNF>
                    <dhEmi>2026-08-20T15:00:00-03:00</dhEmi>
                    <tpAmb>1</tpAmb>
                  </ide>
                  <emit>
                    <CNPJ>{SyntheticIssuerCnpj}</CNPJ>
                    <CRT>1</CRT>
                  </emit>
                  <dest>
                    <CPF>{cpf}</CPF>
                    <xNome>Destinatario Fixture Sintetico</xNome>
                  </dest>
                  <det nItem="1">
                    <prod>
                      <cProd>{cProd}</cProd>
                      <NCM>49019900</NCM>
                      <CFOP>5102</CFOP>
                      <uCom>UN</uCom>
                      <qCom>{qtyText}</qCom>
                      <vUnCom>{unitText}</vUnCom>
                      <vProd>{totalText}</vProd>
                      <xPed>{xPed}</xPed>
                    </prod>
                  </det>
                  <total>
                    <ICMSTot>
                      <vNF>{totalText}</vNF>
                    </ICMSTot>
                  </total>
                </infNFe>
              </NFe>
              {protocolXml}
            </nfeProc>
            """;
    }

    [Fact]
    public void SkuNormalizer_MapsHyphenatedToUpsellerStyle()
    {
        FiscalSkuNormalizer.Normalize("SKU-WAITE-TAROT").Should().Be("SKUWAITETAROT");
        FiscalSkuNormalizer.Normalize("SKUWAITETAROT").Should().Be("SKUWAITETAROT");
        FiscalSkuNormalizer.EqualsNormalized("SKU-WAITE-TAROT", "SKUWAITETAROT").Should().BeTrue();
        FiscalSkuNormalizer.EqualsNormalized("SKU-WAITE-TAROT", "SKU-OTHER").Should().BeFalse();
    }

    [Fact]
    public void Parser_AcceptsNfeProcAuthorized_CStat100()
    {
        var parser = new FiscalInvoiceXmlParser();
        var result = parser.Parse(Encoding.UTF8.GetBytes(BuildSyntheticAuthorizedXml()));
        result.HasAuthorizationEvidence.Should().BeTrue();
        result.ProtocolStatusCode.Should().Be("100");
        result.ChNFe.Should().Be(SyntheticChNFe);
        result.Number.Should().Be("123");
        result.Series.Should().Be("8");
        result.Environment.Should().Be("1");
        result.Model.Should().Be("55");
        result.IssuerCnpj.Should().Be(SyntheticIssuerCnpj);
        result.RecipientDocument.Should().Be(SyntheticCpf);
        result.InvoiceTotal.Should().Be(54.90m);
        result.Items.Should().ContainSingle(i =>
            i.Sku == SyntheticCProdUpsellerStyle
            && i.Quantity == 1m
            && i.ExternalOrderRef == SyntheticXPed);
    }

    [Fact]
    public void Parser_MissingProtocol_IsNotAuthorized()
    {
        var parser = new FiscalInvoiceXmlParser();
        var result = parser.Parse(Encoding.UTF8.GetBytes(
            BuildSyntheticAuthorizedXml(includeProtocol: false)));
        result.HasAuthorizationEvidence.Should().BeFalse();
        result.ChNFe.Should().Be(SyntheticChNFe);
    }

    [Fact]
    public void Parser_IdChNFeDiffersFromProtocol_Rejects()
    {
        var parser = new FiscalInvoiceXmlParser();
        var other = "35260820888888888888888888888888888888888888";
        var act = () => parser.Parse(Encoding.UTF8.GetBytes(
            BuildSyntheticAuthorizedXml(chNFe: SyntheticChNFe, chNFeInId: other)));
        act.Should().Throw<Application.Exceptions.ValidationException>()
            .Which.Errors.Should().ContainKey("chNFe");
    }

    [Fact]
    public void Parser_RejectsMalformedXml()
    {
        var parser = new FiscalInvoiceXmlParser();
        var act = () => parser.Parse(Encoding.UTF8.GetBytes("<not-closed>"));
        act.Should().Throw<Application.Exceptions.ValidationException>();
    }

    [Fact]
    public void Parser_RejectsDtd()
    {
        var parser = new FiscalInvoiceXmlParser();
        var xml = """
            <?xml version="1.0"?>
            <!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe"><NFe>&xxe;</NFe></nfeProc>
            """;
        var act = () => parser.Parse(Encoding.UTF8.GetBytes(xml));
        act.Should().Throw<Application.Exceptions.ValidationException>();
    }

    [Fact]
    public void Parser_RejectsMissingInfNFe()
    {
        var parser = new FiscalInvoiceXmlParser();
        var xml = """<?xml version="1.0"?><root><nNF>1</nNF></root>""";
        var act = () => parser.Parse(Encoding.UTF8.GetBytes(xml));
        act.Should().Throw<Application.Exceptions.ValidationException>();
    }

    [Fact]
    public void Parser_RejectsInvalidChNFeLength()
    {
        var parser = new FiscalInvoiceXmlParser();
        var xml = BuildSyntheticAuthorizedXml(chNFe: "12345", chNFeInId: "12345");
        var act = () => parser.Parse(Encoding.UTF8.GetBytes(xml));
        act.Should().Throw<Application.Exceptions.ValidationException>()
            .Which.Errors.Should().ContainKey("chNFe");
    }

    [Fact]
    public void Parser_RejectsOversizePayload()
    {
        var parser = new FiscalInvoiceXmlParser();
        var huge = Encoding.UTF8.GetBytes("<root>" + new string('a', FiscalInvoiceXmlParser.DefaultMaxXmlBytes) + "</root>");
        var act = () => parser.Parse(huge);
        act.Should().Throw<Application.Exceptions.ValidationException>();
    }

    [Fact]
    public void Fixture_ContainsNoSignatureAndUsesSyntheticKeyOnly()
    {
        var xml = BuildSyntheticAuthorizedXml();
        xml.Should().Contain($"Id=\"NFe{SyntheticChNFe}\"");
        xml.Should().Contain($"<chNFe>{SyntheticChNFe}</chNFe>");
        xml.Should().NotContain("X509Certificate");
        xml.Should().NotContain("SignatureValue");
        xml.Should().NotContain("<Signature");
        xml.Should().Contain("xmlns=\"http://www.portalfiscal.inf.br/nfe\"");
    }

    [Fact]
    public void Match_DocumentPlusNormalizedSkuQuantity()
    {
        var order = BaseOrder(total: 54.90m, sku: SyntheticSku, qty: 1);
        var ok = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = SyntheticCpf,
            InvoiceTotal = 99.99m, // total divergente — itens devem prevalecer
            Items = [new FiscalInvoiceParsedItem(SyntheticCProdUpsellerStyle, 1)],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, ok);
        act.Should().NotThrow();
    }

    [Fact]
    public void Match_DifferentSku_FailsEvenIfTotalMatches()
    {
        var order = BaseOrder(total: 54.90m, sku: SyntheticSku, qty: 1);
        var bad = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = SyntheticCpf,
            InvoiceTotal = 54.90m,
            Items = [new FiscalInvoiceParsedItem("SKU-OUTRO-PRODUTO", 1)],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, bad);
        act.Should().Throw<Application.Exceptions.ValidationException>()
            .Which.Errors.Should().ContainKey("match");
    }

    [Fact]
    public void Match_DocumentPlusTotal_FallbackWhenNoXmlItems()
    {
        var order = BaseOrder(total: 54.90m, sku: SyntheticSku, qty: 1);
        var ok = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = SyntheticCpf,
            InvoiceTotal = 54.90m,
            Items = [],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, ok);
        act.Should().NotThrow();
    }

    [Fact]
    public void Match_TotalDivergence_FailsOnTotalFallback()
    {
        var order = BaseOrder(total: 54.90m, sku: null, qty: 1);
        order.Items =
        [
            new OrderItem { Sku = null, Quantity = 1, ProductName = "T", UnitPrice = 54.90m, LineTotal = 54.90m }
        ];
        var bad = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = SyntheticCpf,
            InvoiceTotal = 10.00m,
            Items = [],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, bad);
        act.Should().Throw<Application.Exceptions.ValidationException>()
            .Which.Errors.Should().ContainKey("match");
    }

    [Fact]
    public void Match_DifferentDocument_Rejects()
    {
        var order = BaseOrder(total: 54.90m, sku: SyntheticSku, qty: 1);
        var bad = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = "00000000000",
            InvoiceTotal = 54.90m,
            Items = [new FiscalInvoiceParsedItem(SyntheticCProdUpsellerStyle, 1)],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, bad);
        act.Should().Throw<Application.Exceptions.ValidationException>()
            .Which.Errors.Should().ContainKey("recipient");
    }

    [Fact]
    public void Match_XPedDifferentFromOrderNumber_DoesNotBlock()
    {
        var order = BaseOrder(total: 54.90m, sku: SyntheticSku, qty: 1);
        order.OrderNumber = "TESTE-NFE-003";
        var ok = new FiscalInvoiceParseResult
        {
            ChNFe = SyntheticChNFe,
            RecipientDocument = SyntheticCpf,
            InvoiceTotal = 54.90m,
            Items =
            [
                new FiscalInvoiceParsedItem(
                    SyntheticCProdUpsellerStyle,
                    1,
                    ExternalOrderRef: "UPAYHF010007")
            ],
            HasAuthorizationEvidence = true
        };
        var act = () => FiscalInvoiceImportService.ValidateOrderMatch(order, ok);
        act.Should().NotThrow();
    }

    private static string NewChNFe()
    {
        Span<char> digits = stackalloc char[44];
        "35260820".AsSpan().CopyTo(digits);
        var n = Guid.NewGuid().ToByteArray();
        for (var i = 8; i < 44; i++)
            digits[i] = (char)('0' + (n[i % n.Length] % 10));
        return new string(digits);
    }

    private static Order BaseOrder(decimal total, string? sku, int qty) =>
        new()
        {
            CustomerCpf = SyntheticCpf,
            Total = total,
            OrderNumber = "TESTE-INTERNAL",
            Items =
            [
                new OrderItem
                {
                    Sku = sku,
                    Quantity = qty,
                    ProductName = "T",
                    UnitPrice = total,
                    LineTotal = total
                }
            ]
        };

    [Fact]
    public async Task Import_ValidXml_EncryptsAndDoesNotExposeXmlInResponse()
    {
        var ch = NewChNFe();
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        using var content = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: ch));
        var response = await _client.PostAsync(
            $"/api/admin/orders/{orderId}/fiscal-invoices/xml",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("<nfeProc");
        body.Should().NotContain(SyntheticCpf);
        body.Should().NotContain("XmlCipher");
        body.Should().NotContain(ch);

        var dto = await response.Content.ReadFromJsonAsync<FiscalInvoiceImportResultDto>();
        dto!.Status.Should().Be(FiscalInvoiceStatus.Authorized);
        dto.MaskedChNFe.Should().EndWith(ch[^6..]);
        dto.MaskedChNFe.Should().NotBe(ch);
        dto.Number.Should().Be("123");
        dto.Series.Should().Be("8");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var enc = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IIntegrationsEncryptionService>();
        var row = await db.FiscalInvoices.SingleAsync(f => f.OrderId == orderId);
        row.XmlCipher.Should().NotBeNullOrWhiteSpace();
        row.XmlCipher.Should().NotContain("<chNFe>");
        var plain = enc.Decrypt(row.XmlCipher);
        plain.Should().Contain(ch);
        row.XmlSha256.Should().HaveLength(64);
        row.Status.Should().Be(FiscalInvoiceStatus.Authorized);
        row.Source.Should().Be(FiscalInvoiceSource.ManualUpload);
    }

    [Fact]
    public async Task Import_SameXmlSameOrder_IsIdempotent()
    {
        var ch = NewChNFe();
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var xml = BuildSyntheticAuthorizedXml(chNFe: ch);

        using (var c1 = BuildMultipart(xml))
        {
            var r1 = await _client.PostAsync($"/api/admin/orders/{orderId}/fiscal-invoices/xml", c1);
            r1.StatusCode.Should().Be(HttpStatusCode.OK);
            var d1 = await r1.Content.ReadFromJsonAsync<FiscalInvoiceImportResultDto>();
            d1!.IdempotentReplay.Should().BeFalse();
        }

        using (var c2 = BuildMultipart(xml))
        {
            var r2 = await _client.PostAsync($"/api/admin/orders/{orderId}/fiscal-invoices/xml", c2);
            r2.StatusCode.Should().Be(HttpStatusCode.OK);
            var d2 = await r2.Content.ReadFromJsonAsync<FiscalInvoiceImportResultDto>();
            d2!.IdempotentReplay.Should().BeTrue();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        (await db.FiscalInvoices.CountAsync(f => f.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Import_SameChNFeOtherOrder_Conflicts()
    {
        var ch = NewChNFe();
        var orderA = await SeedPaidOrderAsync(total: 54.90m, orderNumberPrefix: "FA");
        var orderB = await SeedPaidOrderAsync(total: 54.90m, orderNumberPrefix: "FB");
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        using (var c1 = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: ch)))
        {
            (await _client.PostAsync($"/api/admin/orders/{orderA}/fiscal-invoices/xml", c1))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var c2 = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: ch));
        var r2 = await _client.PostAsync($"/api/admin/orders/{orderB}/fiscal-invoices/xml", c2);
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Import_WithoutAdmin_IsUnauthorized()
    {
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        _client.DefaultRequestHeaders.Authorization = null;
        using var content = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: NewChNFe()));
        var response = await _client.PostAsync(
            $"/api/admin/orders/{orderId}/fiscal-invoices/xml",
            content);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Import_Oversize_ReturnsBadRequest()
    {
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var big = "<root>" + new string('x', (int)FiscalInvoiceImportService.MaxUploadBytes + 10) + "</root>";
        using var content = BuildMultipart(big);
        var response = await _client.PostAsync(
            $"/api/admin/orders/{orderId}/fiscal-invoices/xml",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Import_DoesNotCallJ3()
    {
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        using var scope = _factory.Services.CreateScope();
        var fakeJ3 = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var before = fakeJ3.CreateCallCount;

        using var content = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: NewChNFe()));
        (await _client.PostAsync($"/api/admin/orders/{orderId}/fiscal-invoices/xml", content))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        fakeJ3.CreateCallCount.Should().Be(before);
    }

    [Fact]
    public async Task AdminDetail_ShowsFiscalSummaryWithoutFullChNFe()
    {
        var ch = NewChNFe();
        var orderId = await SeedPaidOrderAsync(total: 54.90m);
        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        using (var content = BuildMultipart(BuildSyntheticAuthorizedXml(chNFe: ch)))
        {
            (await _client.PostAsync($"/api/admin/orders/{orderId}/fiscal-invoices/xml", content))
                .EnsureSuccessStatusCode();
        }

        var detail = await _client.GetFromJsonAsync<Application.DTOs.Admin.AdminOrderDetailDto>(
            $"/api/admin/orders/{orderId}");
        detail!.Fiscal.FiscalStatus.Should().Be(FiscalInvoiceStatus.Authorized);
        detail.Fiscal.MaskedChNFe.Should().NotBe(ch);
        detail.Fiscal.MaskedChNFe.Should().EndWith(ch[^6..]);
        detail.Items.Should().Contain(i => i.Sku == SyntheticSku);
    }

    private static MultipartFormDataContent BuildMultipart(string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "nfe-synthetic.xml");
        return form;
    }

    private async Task<Guid> SeedPaidOrderAsync(
        decimal total,
        string orderNumberPrefix = "FISC")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");

        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Orders.Add(new Order
        {
            Id = orderId,
            OrderNumber = $"{orderNumberPrefix}-{Guid.NewGuid():N}"[..16],
            UserId = user.Id,
            Status = OrderStatus.PaymentApproved,
            Subtotal = total,
            Discount = 0m,
            ShippingPrice = 0m,
            Total = total,
            ShippingMethodId = "manual",
            ShippingMethodName = "Manual",
            ShippingProvider = string.Empty,
            ShipCep = "01310100",
            ShipStreet = "Av Paulista",
            ShipNumber = "1000",
            ShipNeighborhood = "Bela Vista",
            ShipCity = "São Paulo",
            ShipState = "SP",
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Cliente Fiscal Teste",
            CustomerEmail = "cliente@esotera.demo",
            CustomerPhone = "11999990000",
            CustomerCpf = SyntheticCpf,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductName = "Tarô",
                    Variation = "Somente Tarô",
                    Sku = SyntheticSku,
                    UnitPrice = total,
                    Quantity = 1,
                    LineTotal = total
                }
            ]
        });
        await db.SaveChangesAsync();
        return orderId;
    }
}
