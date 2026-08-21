using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

public class UpSellerExportTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaiteInicianteId =
        Guid.Parse("11111111-1111-1111-1111-111111111107");

    private static readonly XNamespace SsNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public UpSellerExportTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void EmbeddedTemplate_HasExpectedSha256()
    {
        var bytes = UpSellerOrderExportService.ReadEmbeddedTemplateBytes();
        UpSellerOrderExportService.ComputeSha256Hex(bytes)
            .Should().Be(UpSellerOrderExportService.ExpectedTemplateSha256);
    }

    [Fact]
    public async Task PaidOrder_WithSku_GeneratesXlsx_AndPreservesZipEntries()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"US-{Guid.NewGuid():N}".Substring(0, 16),
            items:
            [
                ("Tarô", "ESOTERA-TARO-001", 54.90m, 1, "Somente Tarô")
            ],
            shipState: "SP",
            shipCity: "São Paulo",
            shipStreet: "Av Paulista",
            shipNumber: "1000",
            shipNeighborhood: "Bela Vista",
            shipCep: "01310100",
            shippingPrice: 12.50m,
            discount: 5m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var template = UpSellerOrderExportService.ReadEmbeddedTemplateBytes();
        var changed = UpSellerOrderExportService.DiffChangedEntries(template, bytes);
        changed.Should().NotBeEmpty();
        changed.Should().OnlyContain(e => UpSellerOrderExportService.IsAllowedChangedEntry(e));

        UpSellerOrderExportService.TryReadIcv(bytes)
            .Should().Be(UpSellerOrderExportService.ExpectedIcvValue);

        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        var shared = UpSellerXlsxReader.ReadSharedStrings(bytes);

        UpSellerXlsxReader.GetCellText(sheet, shared, "B4").Should().Be("Loja Padrão");
        UpSellerXlsxReader.GetCellText(sheet, shared, "E4").Should().Be("Não");
        UpSellerXlsxReader.CellExists(sheet, "D4").Should().BeFalse("D4 deve ser removida sem observação/cupom");
        UpSellerXlsxReader.GetCellText(sheet, shared, "F4").Should().Be("Cliente Snapshot UpSeller");
        UpSellerXlsxReader.GetCellText(sheet, shared, "M4").Should().Be("São Paulo");
        UpSellerXlsxReader.GetCellText(sheet, shared, "N4").Should().Be("São Paulo");
        UpSellerXlsxReader.GetCellText(sheet, shared, "Q4").Should().Be("Av Paulista");
        UpSellerXlsxReader.GetCellText(sheet, shared, "S4").Should().Be("My Warehouse");
        UpSellerXlsxReader.GetCellText(sheet, shared, "T4").Should().Be("ESOTERA-TARO-001");
        UpSellerXlsxReader.GetCellText(sheet, shared, "AO4").Should().Be("PIX");

        UpSellerXlsxReader.GetCellNumber(sheet, "U4").Should().Be(1m);
        UpSellerXlsxReader.GetCellNumber(sheet, "V4").Should().Be(54.90m);
        UpSellerXlsxReader.GetCellNumber(sheet, "W4").Should().Be(2m);
        UpSellerXlsxReader.GetCellNumber(sheet, "AB4").Should().Be(1m);
        UpSellerXlsxReader.GetCellNumber(sheet, "AP4").Should().Be(12.50m);
        UpSellerXlsxReader.GetCellNumber(sheet, "AQ4").Should().Be(5m);
        UpSellerXlsxReader.CellExists(sheet, "AR4").Should().BeFalse();

        UpSellerXlsxReader.IsNumericCell(sheet, "W4").Should().BeTrue();
        UpSellerXlsxReader.IsNumericCell(sheet, "U4").Should().BeTrue();
        UpSellerXlsxReader.IsNumericCell(sheet, "V4").Should().BeTrue();
        UpSellerXlsxReader.IsNumericCell(sheet, "AB4").Should().BeTrue();

        // Headers B3:AR3 idênticos ao template (via shared strings do template vs gerado nas células de header)
        var templateSheet = UpSellerXlsxReader.ReadSheetXml(template);
        var templateShared = UpSellerXlsxReader.ReadSharedStrings(template);
        for (var col = 2; col <= 44; col++)
        {
            var addr = UpSellerXlsxReader.ColumnName(col) + "3";
            UpSellerXlsxReader.GetCellText(sheet, shared, addr)
                .Should().Be(UpSellerXlsxReader.GetCellText(templateSheet, templateShared, addr), addr);
        }
    }

    [Fact]
    public async Task OrderWithoutSku_FailsWithClearError()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"US-NOSKU-{Guid.NewGuid():N}".Substring(0, 18),
            items:
            [
                ("Produto sem SKU", sku: null, 10m, 1, null)
            ]);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SKU");
    }

    [Fact]
    public async Task MultipleItems_ShareStoreAndOrderNumber()
    {
        var orderNumber = $"US-MULTI-{Guid.NewGuid():N}".Substring(0, 18);
        var orderId = await SeedPaidOrderAsync(
            orderNumber,
            items:
            [
                ("Item A", "SKU-A-001", 10m, 2, null),
                ("Item B", "SKU-B-002", 20m, 1, null)
            ],
            shippingPrice: 8m,
            discount: 1m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        var shared = UpSellerXlsxReader.ReadSharedStrings(bytes);

        UpSellerXlsxReader.GetCellText(sheet, shared, "B4").Should().Be("Loja Padrão");
        UpSellerXlsxReader.GetCellText(sheet, shared, "B5").Should().Be("Loja Padrão");
        UpSellerXlsxReader.GetCellText(sheet, shared, "C4").Should().Be(orderNumber);
        UpSellerXlsxReader.GetCellText(sheet, shared, "C5").Should().Be(orderNumber);
        UpSellerXlsxReader.GetCellText(sheet, shared, "E4").Should().Be("Não");
        UpSellerXlsxReader.GetCellText(sheet, shared, "E5").Should().Be("Não");
        UpSellerXlsxReader.CellExists(sheet, "D4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "D5").Should().BeFalse();

        var skus = new[]
        {
            UpSellerXlsxReader.GetCellText(sheet, shared, "T4"),
            UpSellerXlsxReader.GetCellText(sheet, shared, "T5")
        };
        skus.Should().BeEquivalentTo(["SKU-A-001", "SKU-B-002"]);

        UpSellerXlsxReader.GetCellNumber(sheet, "AP4").Should().Be(8m);
        UpSellerXlsxReader.CellExists(sheet, "AP5").Should().BeFalse("frete só na 1ª linha; zero → célula ausente");
        UpSellerXlsxReader.GetCellNumber(sheet, "AQ4").Should().Be(1m);
        UpSellerXlsxReader.CellExists(sheet, "AQ5").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AR4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AR5").Should().BeFalse();
    }

    [Fact]
    public async Task MoreThanThreeItems_FailsExplicitly()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"US4{Guid.NewGuid():N}"[..16],
            items:
            [
                ("A", "SKU-1", 1m, 1, null),
                ("B", "SKU-2", 1m, 1, null),
                ("C", "SKU-3", 1m, 1, null),
                ("D", "SKU-4", 1m, 1, null)
            ]);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("3 itens");
        body.Should().ContainEquivalentOf("homologada");
    }

    [Fact]
    public async Task SharedString_UsedByMultipleTemplateCells_IsNotClobbered()
    {
        // Template canônico: M4 e N4 compartilhavam o mesmo SI ("São Paulo").
        // Estado SP → "São Paulo", cidade diferente → deve criar SI nova sem corromper a outra.
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USS{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-SHARE-1", 10m, 1, null)],
            shipState: "SP",
            shipCity: "Campinas",
            shipStreet: "Rua A",
            shipNumber: "1",
            shipNeighborhood: "Centro",
            shipCep: "13010000");

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        var shared = UpSellerXlsxReader.ReadSharedStrings(bytes);

        UpSellerXlsxReader.GetCellText(sheet, shared, "M4").Should().Be("São Paulo");
        UpSellerXlsxReader.GetCellText(sheet, shared, "N4").Should().Be("Campinas");

        var sst = UpSellerXlsxReader.ReadSharedDoc(bytes).Root!;
        var unique = sst.Elements(SsNs + "si").Count();
        var uniqueAttr = (string?)sst.Attribute("uniqueCount");
        uniqueAttr.Should().Be(unique.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task OptionalMoney_ZeroDiscount_OmitsCell()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USZ{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-ZDISC", 10m, 1, null)],
            shippingPrice: 18.9m,
            discount: 0m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);

        UpSellerXlsxReader.GetCellNumber(sheet, "AP4").Should().Be(18.9m);
        UpSellerXlsxReader.CellExists(sheet, "AQ4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AR4").Should().BeFalse();
        sheet.Should().NotContain("<c r=\"AQ4\"");
        sheet.Should().NotContain("<c r=\"AR4\"");
    }

    [Fact]
    public async Task OptionalMoney_PositiveDiscount_WritesNumber()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USP{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-PDISC", 10m, 1, null)],
            shippingPrice: 5m,
            discount: 2.5m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);

        UpSellerXlsxReader.IsNumericCell(sheet, "AQ4").Should().BeTrue();
        UpSellerXlsxReader.GetCellNumber(sheet, "AQ4").Should().Be(2.5m);
    }

    [Fact]
    public async Task OptionalMoney_ZeroBuyerFreight_OmitsCell()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USF{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-ZFRETE", 10m, 1, null)],
            shippingPrice: 0m,
            discount: 0m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);

        UpSellerXlsxReader.CellExists(sheet, "AP4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AQ4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AR4").Should().BeFalse();
    }

    [Fact]
    public async Task OptionalMoney_PositiveBuyerFreight_WritesNumber()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USB{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-PFRETE", 10m, 1, null)],
            shippingPrice: 18.9m,
            discount: 0m);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();
        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);

        UpSellerXlsxReader.IsNumericCell(sheet, "AP4").Should().BeTrue();
        UpSellerXlsxReader.GetCellNumber(sheet, "AP4").Should().Be(18.9m);
        UpSellerXlsxReader.CellExists(sheet, "AQ4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "AR4").Should().BeFalse();
    }

    [Fact]
    public async Task InvoiceRequired_IsExactTemplateLiteral_Nao()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USN{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-NAO-1", 10m, 1, null)]);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();

        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        var shared = UpSellerXlsxReader.ReadSharedStrings(bytes);
        var e4 = UpSellerXlsxReader.GetCellText(sheet, shared, "E4");
        e4.Should().Be("Não");
        e4.Should().NotBe("NÃO");
        e4.Should().NotBe("NAO");
        e4.Should().NotBe("false");
        UpSellerXlsxReader.CellExists(sheet, "H4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "I4").Should().BeFalse();
    }

    [Fact]
    public async Task InvoiceRequired_Sim_WithCpf_FillsTaxTypeAndNumber()
    {
        await using var db = CreateInMemoryDb();
        var orderId = await SeedOrderForDirectExportAsync(
            db,
            orderNumber: "TESTE-NFE-SIM-CPF",
            customerCpf: "529.982.247-25");

        var svc = new UpSellerOrderExportService(db, Options.Create(new UpSellerOptions
        {
            InvoiceRequired = "Sim",
            StoreName = "Loja Padrão",
            WarehouseName = "My Warehouse",
            ShippingCostMethod = "2",
            PackageQuantity = 1
        }));

        var file = await svc.ExportOrderAsync(orderId);
        var template = UpSellerOrderExportService.ReadEmbeddedTemplateBytes();
        var changed = UpSellerOrderExportService.DiffChangedEntries(template, file.Content);
        changed.Should().OnlyContain(e => UpSellerOrderExportService.IsAllowedChangedEntry(e));
        UpSellerOrderExportService.TryReadIcv(file.Content)
            .Should().Be(UpSellerOrderExportService.ExpectedIcvValue);

        var sheet = UpSellerXlsxReader.ReadSheetXml(file.Content);
        var shared = UpSellerXlsxReader.ReadSharedStrings(file.Content);
        UpSellerXlsxReader.GetCellText(sheet, shared, "E4").Should().Be("Sim");
        UpSellerXlsxReader.GetCellText(sheet, shared, "H4").Should().Be("CPF");
        UpSellerXlsxReader.GetCellText(sheet, shared, "I4").Should().Be("52998224725");
        UpSellerXlsxReader.CellExists(sheet, "J4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "K4").Should().BeFalse();
        UpSellerXlsxReader.GetCellText(sheet, shared, "F4").Should().Be("Cliente Homolog NFe");
        UpSellerXlsxReader.GetCellText(sheet, shared, "L4").Should().Be("01310-100");
        UpSellerXlsxReader.GetCellText(sheet, shared, "M4").Should().Be("São Paulo");
        UpSellerXlsxReader.GetCellText(sheet, shared, "N4").Should().Be("São Paulo");
        UpSellerXlsxReader.GetCellText(sheet, shared, "O4").Should().Be("Bela Vista");
    }

    [Fact]
    public async Task InvoiceRequired_Sim_WithoutCpf_FailsExplicitly()
    {
        await using var db = CreateInMemoryDb();
        var orderId = await SeedOrderForDirectExportAsync(
            db,
            orderNumber: "TESTE-NFE-SIM-NOCPF",
            customerCpf: null);

        var svc = new UpSellerOrderExportService(db, Options.Create(new UpSellerOptions
        {
            InvoiceRequired = "Sim"
        }));

        var act = () => svc.ExportOrderAsync(orderId);
        var ex = await act.Should().ThrowAsync<Application.Exceptions.ValidationException>();
        ex.Which.Errors.Should().ContainKey("customerCpf");
    }

    [Fact]
    public async Task InvoiceRequired_Nao_LeavesTaxColumnsEmpty()
    {
        await using var db = CreateInMemoryDb();
        var orderId = await SeedOrderForDirectExportAsync(
            db,
            orderNumber: "TESTE-NFE-NAO",
            customerCpf: "52998224725");

        var svc = new UpSellerOrderExportService(db, Options.Create(new UpSellerOptions
        {
            InvoiceRequired = "Não"
        }));

        var file = await svc.ExportOrderAsync(orderId);
        var template = UpSellerOrderExportService.ReadEmbeddedTemplateBytes();
        UpSellerOrderExportService.DiffChangedEntries(template, file.Content)
            .Should().OnlyContain(e => UpSellerOrderExportService.IsAllowedChangedEntry(e));

        var sheet = UpSellerXlsxReader.ReadSheetXml(file.Content);
        var shared = UpSellerXlsxReader.ReadSharedStrings(file.Content);
        UpSellerXlsxReader.GetCellText(sheet, shared, "E4").Should().Be("Não");
        UpSellerXlsxReader.CellExists(sheet, "H4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "I4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "J4").Should().BeFalse();
        UpSellerXlsxReader.CellExists(sheet, "K4").Should().BeFalse();
    }

    private static EsoteraDbContext CreateInMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<EsoteraDbContext>()
            .UseInMemoryDatabase("upseller-nfe-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new EsoteraDbContext(opts);
    }

    private static async Task<Guid> SeedOrderForDirectExportAsync(
        EsoteraDbContext db,
        string orderNumber,
        string? customerCpf)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Name = "Cliente Homolog NFe",
            Email = $"{orderNumber.ToLowerInvariant()}@local.invalid",
            PasswordHash = "x",
            Role = UserRole.Customer,
            CreatedAtUtc = DateTime.UtcNow
        });

        var orderId = Guid.NewGuid();
        const decimal unit = 54.90m;
        var now = DateTime.UtcNow;
        db.Orders.Add(new Order
        {
            Id = orderId,
            OrderNumber = orderNumber,
            UserId = userId,
            Status = OrderStatus.PaymentApproved,
            Subtotal = unit,
            Discount = 0m,
            ShippingPrice = 0m,
            Total = unit,
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
            CustomerName = "Cliente Homolog NFe",
            CustomerEmail = $"{orderNumber.ToLowerInvariant()}@local.invalid",
            CustomerPhone = "11987654321",
            CustomerCpf = customerCpf,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductName = "Rider Waite Tarô",
                    Variation = "Somente Tarô",
                    Sku = "SKU-WAITE-TAROT",
                    UnitPrice = unit,
                    Quantity = 1,
                    LineTotal = unit
                }
            ]
        });
        await db.SaveChangesAsync();
        return orderId;
    }

    [Fact]
    public async Task Observation_D4_IsAbsent_WhenNoCoupon()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USD{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-D4-1", 10m, 1, null)]);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();

        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        UpSellerXlsxReader.CellExists(sheet, "D4").Should().BeFalse();
        // Não pode aparecer o índice antigo (52) como valor numérico solto.
        var raw = UpSellerXlsxReader.FindCell(sheet, "D4");
        raw.Should().BeNull();
        sheet.Should().NotMatchRegex(@"<c\b[^>]*\br=""D4""[^>]*>\s*<v>52</v>");
    }

    [Fact]
    public async Task Observation_D4_WritesCouponNote_WhenPresent()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USC{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-CUP-1", 10m, 1, null)],
            couponCode: "PROMO10");

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);
        var bytes = await (await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export"))
            .Content.ReadAsByteArrayAsync();

        var sheet = UpSellerXlsxReader.ReadSheetXml(bytes);
        var shared = UpSellerXlsxReader.ReadSharedStrings(bytes);
        UpSellerXlsxReader.GetCellText(sheet, shared, "D4").Should().Be("Cupom PROMO10");
        UpSellerXlsxReader.IsNumericCell(sheet, "D4").Should().BeFalse();
    }

    [Fact]
    public async Task AwaitingPayment_IsNotEligible()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"US-WAIT-{Guid.NewGuid():N}".Substring(0, 16),
            items: [("X", "SKU-X", 1m, 1, null)],
            status: OrderStatus.AwaitingPayment);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task TerminalOrShippedStatuses_AreNotEligible(string status)
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USX{Guid.NewGuid():N}"[..16],
            items: [("X", "SKU-X", 1m, 1, null)],
            status: status);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Preparing_IsEligible()
    {
        var orderId = await SeedPaidOrderAsync(
            orderNumber: $"USP{Guid.NewGuid():N}"[..16],
            items: [("Prep", "SKU-PREP-1", 10m, 1, null)],
            status: OrderStatus.Preparing);

        var admin = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, admin);

        var response = await _client.GetAsync($"/api/admin/orders/{orderId}/upseller-export");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateOrder_WithVariation_SnapshotsSku()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"upsellersku{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var request = new CreateOrderRequest(
            [new CreateOrderItemRequest(ProductWaiteInicianteId, 1, "var-somente-taro")],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", true),
            null,
            "melhor_economico",
            "pix",
            null,
            null);

        var response = await TestHelpers.PostOrderAsync(_client, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order.Should().NotBeNull();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var item = await db.OrderItems.AsNoTracking()
            .SingleAsync(i => i.OrderId == order!.Id);
        item.Sku.Should().Be("SKU-WAITE-TAROT");
        item.Variation.Should().Be("Somente Tarô");
    }

    private async Task<Guid> SeedPaidOrderAsync(
        string orderNumber,
        (string Name, string? Sku, decimal UnitPrice, int Qty, string? Variation)[] items,
        string shipState = "RJ",
        string shipCity = "Rio de Janeiro",
        string shipStreet = "Rua do Teste",
        string shipNumber = "10",
        string shipNeighborhood = "Centro",
        string shipCep = "20040020",
        decimal shippingPrice = 0m,
        decimal discount = 0m,
        string status = OrderStatus.PaymentApproved,
        string? couponCode = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");

        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var subtotal = items.Sum(i => i.UnitPrice * i.Qty);
        var order = new Order
        {
            Id = orderId,
            OrderNumber = orderNumber,
            UserId = user.Id,
            Status = status,
            Subtotal = subtotal,
            Discount = discount,
            ShippingPrice = shippingPrice,
            Total = subtotal - discount + shippingPrice,
            CouponCode = couponCode,
            ShippingMethodId = "melhor_economico",
            ShippingMethodName = "Econômico",
            ShippingProvider = "Melhor Envio",
            ShippingEstimatedDays = 5,
            ShipCep = shipCep,
            ShipStreet = shipStreet,
            ShipNumber = shipNumber,
            ShipNeighborhood = shipNeighborhood,
            ShipCity = shipCity,
            ShipState = shipState,
            PaymentMethod = "pix",
            PaymentStatus = status == OrderStatus.PaymentApproved ? "approved" : "pending",
            CustomerName = "Cliente Snapshot UpSeller",
            CustomerEmail = "cliente@esotera.demo",
            CustomerPhone = "21988887777",
            CustomerCpf = "52998224725",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items = items.Select(i => new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductName = i.Name,
                UnitPrice = i.UnitPrice,
                Quantity = i.Qty,
                Variation = i.Variation,
                Sku = i.Sku,
                LineTotal = i.UnitPrice * i.Qty
            }).ToList()
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return orderId;
    }
}

internal static class UpSellerXlsxReader
{
    private static readonly XNamespace Ns =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static string ReadSheetXml(byte[] xlsx)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<string> ReadSharedStrings(byte[] xlsx)
    {
        var doc = ReadSharedDoc(xlsx);
        return doc.Root!.Elements(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)))
            .ToList();
    }

    public static XDocument ReadSharedDoc(byte[] xlsx)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/sharedStrings.xml")!.Open();
        return XDocument.Load(stream);
    }

    public static string? GetCellText(string sheetXml, IReadOnlyList<string> shared, string address)
    {
        var raw = FindCell(sheetXml, address);
        if (raw is null) return null;
        if (raw.Contains("t=\"s\"", StringComparison.Ordinal))
        {
            var idx = int.Parse(Regex.Match(raw, @"<v>(\d+)</v>").Groups[1].Value, CultureInfo.InvariantCulture);
            return idx >= 0 && idx < shared.Count ? shared[idx] : null;
        }

        var inline = Regex.Match(raw, @"<t[^>]*>([^<]*)</t>");
        if (inline.Success) return inline.Groups[1].Value;
        var v = Regex.Match(raw, @"<v>([^<]*)</v>");
        return v.Success ? v.Groups[1].Value : "";
    }

    public static decimal GetCellNumber(string sheetXml, string address)
    {
        var raw = FindCell(sheetXml, address) ?? throw new InvalidOperationException($"cell {address} missing");
        if (raw.Contains("t=\"s\"", StringComparison.Ordinal))
            throw new InvalidOperationException($"cell {address} is shared string, expected number");
        var v = Regex.Match(raw, @"<v>([^<]*)</v>").Groups[1].Value;
        return decimal.Parse(v, CultureInfo.InvariantCulture);
    }

    public static bool IsNumericCell(string sheetXml, string address)
    {
        var raw = FindCell(sheetXml, address);
        return raw is not null
            && !raw.Contains("t=\"s\"", StringComparison.Ordinal)
            && Regex.IsMatch(raw, @"<v>\s*-?[0-9]+(?:\.[0-9]+)?\s*</v>");
    }

    public static bool CellExists(string sheetXml, string address) =>
        FindCell(sheetXml, address) is not null;

    public static string? FindCell(string sheetXml, string address)
    {
        var m = Regex.Match(
            sheetXml,
            $@"<c\b[^>]*\br=""{Regex.Escape(address)}""[^>]*/>|<c\b[^>]*\br=""{Regex.Escape(address)}""[^>]*>[\s\S]*?</c>");
        return m.Success ? m.Value : null;
    }

    public static string ColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var name = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            dividend = (dividend - modulo) / 26;
        }

        return name;
    }
}
