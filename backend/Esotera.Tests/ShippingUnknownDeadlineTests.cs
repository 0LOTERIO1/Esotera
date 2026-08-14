using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Orders;
using Esotera.Application.DTOs.Shipping;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>
/// Passo 2.7 — prazo desconhecido (null) sem sentinels 0/1/-1/999.
/// Passo 3: J3 real já emite null/null via FakeJ3Client (zero rede).
/// </summary>
public class ShippingUnknownDeadlineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid ProductWaitePocketId =
        Guid.Parse("11111111-1111-1111-1111-111111111102");

    // ── 1. Label "Prazo a confirmar" ─────────────────────────────

    [Fact]
    public void QuoteLabel_BothNull_IsPrazoAConfirmar()
    {
        var option = SampleOption(min: null, max: null);
        option.EstimatedDaysLabel.Should().Be("Prazo a confirmar");
    }

    [Fact]
    public void QuoteLabel_EitherNull_IsPrazoAConfirmar()
    {
        SampleOption(min: null, max: 3).EstimatedDaysLabel.Should().Be("Prazo a confirmar");
        SampleOption(min: 2, max: null).EstimatedDaysLabel.Should().Be("Prazo a confirmar");
    }

    [Fact]
    public void QuoteLabel_KnownZero_IsHoje_NotUnknown()
    {
        SampleOption(min: 0, max: 0).EstimatedDaysLabel.Should().Be("Hoje (até o fim do dia)");
    }

    [Fact]
    public void QuoteDto_MapsNullDays_AndLabel()
    {
        var option = SampleOption(min: null, max: null);
        var dto = new ShippingQuoteOptionDto(
            option.ShippingMethodId,
            option.Provider,
            option.Name,
            option.FinalPrice,
            option.OriginalPrice,
            option.EstimatedDaysLabel,
            option.EstimatedDaysMin,
            option.EstimatedDaysMax,
            option.Description,
            option.FreeShippingApplied,
            option.SubsidyApplied);

        dto.EstimatedDays.Should().Be("Prazo a confirmar");
        dto.EstimatedDaysMin.Should().BeNull();
        dto.EstimatedDaysMax.Should().BeNull();
    }

    // ── 2. isSameDay semantics (backend contract for FE) ────────

    [Fact]
    public void SameDay_NullMin_IsNotSameDay()
    {
        // Espelha regra do mapper TS: min == null → false; min === 0 → true.
        IsSameDay(null).Should().BeFalse();
        IsSameDay(0).Should().BeTrue();
        IsSameDay(1).Should().BeFalse();
        IsSameDay(5).Should().BeFalse();
    }

    // ── 3–5. CreateOrder + DTO preservam null ───────────────────

    [Fact]
    public async Task CreateOrder_UnknownDeadline_PersistsNulls_AndDtoPreservesNull()
    {
        // J3 real (Fake coverage=true) já produz EstimatedDays null — sem decorator.
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        ShippingTestHelpers.GetJ3Fake(factory.Services).Reset();

        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            client, $"unk{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await TestHelpers.PostOrderAsync(
            client, BaseRequest(ProductWaitePocketId, shipping: ShippingMethod.J3));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        order.Should().NotBeNull();
        order!.Shipping.EstimatedDays.Should().BeNull();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var entity = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        entity.ShippingEstimatedDays.Should().BeNull();
        entity.ShippingDeliveryMinDays.Should().BeNull();
        entity.ShippingDeliveryMaxDays.Should().BeNull();
    }

    // ── 9. PAC/SEDEX regression ─────────────────────────────────

    [Fact]
    public void MelhorEnvio_KnownDays_LabelsUnchanged()
    {
        SampleOption(min: 5, max: 5, methodId: ShippingMethod.MelhorEconomico)
            .EstimatedDaysLabel.Should().Be("5 dias úteis");
        SampleOption(min: 1, max: 1, methodId: ShippingMethod.MelhorExpresso)
            .EstimatedDaysLabel.Should().Be("1 dia útil");
        SampleOption(min: 3, max: 7, methodId: ShippingMethod.MelhorEconomico)
            .EstimatedDaysLabel.Should().Be("3 a 7 dias úteis");
    }

    [Fact]
    public void MelhorEnvioMapper_PacSedex_StillRequireKnownDays()
    {
        var pac = MelhorEnvioQuoteMapper.TryMapService(new MelhorEnvioRawServiceQuote
        {
            CompanyId = 1,
            ServiceId = 1,
            CompanyName = "Correios",
            ServiceName = "PAC",
            CustomPrice = 18.90m,
            CustomDeliveryTime = 5
        }, DateTime.UtcNow, "sandbox");

        pac.Should().NotBeNull();
        pac!.EstimatedDaysMin.Should().Be(5);
        pac.EstimatedDaysMax.Should().Be(5);
        pac.EstimatedDaysLabel.Should().Be("5 dias úteis");
        pac.ShippingMethodId.Should().Be(ShippingMethod.MelhorEconomico);
    }

    // ── 10. Sem coalescing silencioso no snapshot ───────────────

    [Fact]
    public void OrderService_Source_NoSilentCoalesceOnEstimatedDays()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Path.Combine(root, "Esotera.Infrastructure", "Services", "OrderService.cs");
        File.Exists(path).Should().BeTrue(path);
        var src = File.ReadAllText(path);

        // Trecho do snapshot de frete — não pode forçar 0.
        src.Should().NotContain("EstimatedDaysMax ?? 0");
        src.Should().NotContain("EstimatedDaysMin ?? 0");
        src.Should().NotContain("EstimatedDaysMax.GetValueOrDefault()");
        src.Should().NotContain("EstimatedDaysMin.GetValueOrDefault()");
        src.Should().NotContain("ShippingEstimatedDays = 0");
        src.Should().Contain("ShippingEstimatedDays = estimatedDays");
        src.Should().Contain("ShippingDeliveryMinDays = shippingOption.EstimatedDaysMin");
        src.Should().Contain("ShippingDeliveryMaxDays = shippingOption.EstimatedDaysMax");
    }

    [Fact]
    public void ShippingQuoteService_Source_NoSilentCoalesceOnEstimatedDays()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Path.Combine(root, "Esotera.Infrastructure", "Services", "ShippingQuoteService.cs");
        var src = File.ReadAllText(path);
        src.Should().NotContain("EstimatedDaysMax ?? 0");
        src.Should().NotContain("GetValueOrDefault()");
        src.Should().Contain("option.EstimatedDaysMax");
    }

    // ── helpers ─────────────────────────────────────────────────

    private static bool IsSameDay(int? min) => min is 0;

    private static NormalizedShippingOption SampleOption(
        int? min,
        int? max,
        string methodId = ShippingMethod.J3) =>
        new()
        {
            ShippingMethodId = methodId,
            Provider = ShippingMethod.GetProvider(methodId),
            Name = "Test",
            Description = "Test",
            OriginalPrice = 12m,
            FinalPrice = 12m,
            EstimatedDaysMin = min,
            EstimatedDaysMax = max,
            QuotedAtUtc = DateTime.UtcNow
        };

    private static CreateOrderRequest BaseRequest(Guid productId, string shipping) =>
        new(
            [new CreateOrderItemRequest(productId, 1, null)],
            new OrderAddressInput(
                "01310100", "Av Paulista", "1000", null, "Bela Vista", "São Paulo", "SP",
                IsResidentialAddress: shipping == ShippingMethod.J3 ? true : null),
            null,
            shipping,
            "pix",
            null,
            null);
}
