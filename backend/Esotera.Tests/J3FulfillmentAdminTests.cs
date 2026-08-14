using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esotera.Application.DTOs.Common;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Interfaces;
using Esotera.Application.Shipping;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

/// <summary>Passo 4.4 — admin somente leitura. Zero HTTP/mutation J3.</summary>
public class J3FulfillmentAdminTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public J3FulfillmentAdminTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SetAdminAsync()
    {
        var token = await TestHelpers.GetAdminTokenAsync(_client);
        TestHelpers.SetBearerToken(_client, token);
    }

    [Fact]
    public void AdminController_DoesNotInject_J3ClientsOrProcessor()
    {
        typeof(Esotera.Api.Controllers.AdminJ3FulfillmentsController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should()
            .Equal(typeof(IJ3FulfillmentAdminQueryService));
    }

    [Fact]
    public void Flags_CanRetrySafely_OnlyRetryableFailure()
    {
        J3FulfillmentAdminFlags.CanRetrySafely(J3FulfillmentStatus.RetryableFailure).Should().BeTrue();
        J3FulfillmentAdminFlags.CanRetrySafely(J3FulfillmentStatus.UnknownOutcome).Should().BeFalse();
        J3FulfillmentAdminFlags.CanRetrySafely(J3FulfillmentStatus.Created).Should().BeFalse();
        J3FulfillmentAdminFlags.CanRetrySafely(J3FulfillmentStatus.Pending).Should().BeFalse();
        J3FulfillmentAdminFlags.CanRetrySafely(J3FulfillmentStatus.Processing).Should().BeFalse();
    }

    [Fact]
    public void Flags_IsPossiblyStuck_OnlyOldProcessing()
    {
        var now = DateTime.UtcNow;
        J3FulfillmentAdminFlags.IsPossiblyStuck(
            J3FulfillmentStatus.Processing, now.AddMinutes(-16), now, 15).Should().BeTrue();
        J3FulfillmentAdminFlags.IsPossiblyStuck(
            J3FulfillmentStatus.Processing, now.AddMinutes(-1), now, 15).Should().BeFalse();
        J3FulfillmentAdminFlags.IsPossiblyStuck(
            J3FulfillmentStatus.Pending, now.AddHours(-2), now, 15).Should().BeFalse();
    }

    [Fact]
    public void Flags_NeedsManualReview()
    {
        J3FulfillmentAdminFlags.NeedsManualReview(J3FulfillmentStatus.UnknownOutcome, false).Should().BeTrue();
        J3FulfillmentAdminFlags.NeedsManualReview(J3FulfillmentStatus.Processing, true).Should().BeTrue();
        J3FulfillmentAdminFlags.NeedsManualReview(J3FulfillmentStatus.Created, false).Should().BeFalse();
        J3FulfillmentAdminFlags.NeedsManualReview(J3FulfillmentStatus.Pending, false).Should().BeFalse();
        J3FulfillmentAdminFlags.NeedsManualReview(J3FulfillmentStatus.RetryableFailure, false).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_ListsFulfillments()
    {
        var seeded = await SeedAsync(J3FulfillmentStatus.Pending, tracking: $"TRK-LIST-{Guid.NewGuid():N}"[..20]);
        await SetAdminAsync();

        var response = await _client.GetAsync("/api/admin/j3-fulfillments?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<J3FulfillmentAdminListItemDto>>(JsonOptions);
        page!.Items.Should().Contain(i => i.Id == seeded.Id);
        page.Items.First(i => i.Id == seeded.Id).OrderNumber.Should().Be(seeded.OrderNumber);
    }

    [Fact]
    public async Task Filter_Pending()
    {
        var pending = await SeedAsync(J3FulfillmentStatus.Pending);
        await SeedAsync(J3FulfillmentStatus.Created, tracking: UniqueTracking());
        await SetAdminAsync();

        var page = await ListAsync($"status=pending&pageSize=100");
        page.Items.Should().Contain(i => i.Id == pending.Id);
        page.Items.Should().OnlyContain(i => i.Status == J3FulfillmentStatus.Pending);
    }

    [Fact]
    public async Task Filter_Created()
    {
        var created = await SeedAsync(J3FulfillmentStatus.Created, tracking: UniqueTracking(), j3OrderId: "j3-created");
        await SeedAsync(J3FulfillmentStatus.Pending);
        await SetAdminAsync();

        var page = await ListAsync("status=created&pageSize=100");
        page.Items.Should().Contain(i => i.Id == created.Id);
        page.Items.Should().OnlyContain(i => i.Status == J3FulfillmentStatus.Created);
    }

    [Fact]
    public async Task Filter_UnknownOutcome()
    {
        var unknown = await SeedAsync(J3FulfillmentStatus.UnknownOutcome);
        await SeedAsync(J3FulfillmentStatus.Pending);
        await SetAdminAsync();

        var page = await ListAsync("status=unknown_outcome&pageSize=100");
        page.Items.Should().Contain(i => i.Id == unknown.Id);
        page.Items.Should().OnlyContain(i => i.Status == J3FulfillmentStatus.UnknownOutcome);
    }

    [Fact]
    public async Task Filter_ByOrderId()
    {
        var a = await SeedAsync(J3FulfillmentStatus.Pending);
        await SeedAsync(J3FulfillmentStatus.Pending);
        await SetAdminAsync();

        var page = await ListAsync($"orderId={a.OrderId}");
        page.Items.Should().ContainSingle(i => i.Id == a.Id);
        page.Items[0].OrderId.Should().Be(a.OrderId);
    }

    [Fact]
    public async Task Filter_ByTracking()
    {
        var tracking = UniqueTracking();
        var match = await SeedAsync(J3FulfillmentStatus.Created, tracking: tracking, j3OrderId: "j3-trk");
        await SeedAsync(J3FulfillmentStatus.Created, tracking: UniqueTracking(), j3OrderId: "j3-other");
        await SetAdminAsync();

        var page = await ListAsync($"trackingNumber={tracking}");
        page.Items.Should().ContainSingle();
        page.Items[0].Id.Should().Be(match.Id);
        page.Items[0].J3TrackingNumber.Should().Be(tracking);
    }

    [Fact]
    public async Task Detail_Existing()
    {
        var seeded = await SeedAsync(
            J3FulfillmentStatus.Created,
            tracking: UniqueTracking(),
            j3OrderId: "j3-detail",
            j3OrderCode: "CODE-1",
            deliveryPointId: "dp-1");
        await SetAdminAsync();

        var response = await _client.GetAsync($"/api/admin/j3-fulfillments/{seeded.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<J3FulfillmentAdminDetailDto>(JsonOptions);
        dto!.Id.Should().Be(seeded.Id);
        dto.OrderId.Should().Be(seeded.OrderId);
        dto.OrderNumber.Should().Be(seeded.OrderNumber);
        dto.ShippingMethodId.Should().Be(ShippingMethod.J3);
        dto.PaymentStatus.Should().Be("approved");
        dto.J3OrderId.Should().Be("j3-detail");
        dto.J3OrderCode.Should().Be("CODE-1");
        dto.J3DeliveryPointId.Should().Be("dp-1");
        dto.NeedsManualReview.Should().BeFalse();
        dto.CanRetrySafely.Should().BeFalse();
        dto.IsPossiblyStuck.Should().BeFalse();
    }

    [Fact]
    public async Task Detail_Missing_404()
    {
        await SetAdminAsync();
        var response = await _client.GetAsync($"/api/admin/j3-fulfillments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_GetsForbidden()
    {
        var (token, _) = await TestHelpers.RegisterNewUserAsync(
            _client, $"custj3adm{Guid.NewGuid():N}@test.com");
        TestHelpers.SetBearerToken(_client, token);

        var response = await _client.GetAsync("/api/admin/j3-fulfillments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_GetsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/j3-fulfillments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dto_DoesNotLeak_TokenAddressPhoneOrException()
    {
        var seeded = await SeedAsync(
            J3FulfillmentStatus.RetryableFailure,
            lastErrorCode: "TIMEOUT_UNKNOWN",
            lastErrorAtUtc: DateTime.UtcNow);
        await SetAdminAsync();

        using var scope = _factory.Services.CreateScope();
        var fakeRead = scope.ServiceProvider.GetRequiredService<FakeJ3Client>();
        var fakeMut = scope.ServiceProvider.GetRequiredService<FakeJ3FulfillmentClient>();
        var cov = fakeRead.CoverageCallCount;
        var track = fakeRead.TrackingCallCount;
        var create = fakeMut.CreateCallCount;

        var listRes = await _client.GetAsync($"/api/admin/j3-fulfillments?orderId={seeded.OrderId}");
        var detailRes = await _client.GetAsync($"/api/admin/j3-fulfillments/{seeded.Id}");
        var listJson = await listRes.Content.ReadAsStringAsync();
        var detailJson = await detailRes.Content.ReadAsStringAsync();

        foreach (var json in new[] { listJson, detailJson })
        {
            json.Should().NotContain("fake-j3-token", "token J3 não deve ir ao DTO");
            json.Should().NotContain("Av Paulista");
            json.Should().NotContain("01310100");
            json.Should().NotContain("11988887777");
            json.Should().NotContain("leak@esotera.test");
            json.Should().NotContain("StackTrace");
            json.Should().NotContain("System.InvalidOperationException");
            json.Should().NotContain("createTmsOrders");
            json.Should().NotContain("\"token\"");
            json.Should().NotContain("shipStreet");
            json.Should().NotContain("customerPhone");
            json.Should().NotContain("customerEmail");
            json.Should().NotContain("exception");
        }

        fakeRead.CoverageCallCount.Should().Be(cov);
        fakeRead.TrackingCallCount.Should().Be(track);
        fakeMut.CreateCallCount.Should().Be(create);
    }

    [Fact]
    public async Task RetryableFailure_CanRetrySafely_True()
    {
        var seeded = await SeedAsync(J3FulfillmentStatus.RetryableFailure);
        await SetAdminAsync();
        var dto = await GetDetailAsync(seeded.Id);
        dto.CanRetrySafely.Should().BeTrue();
        dto.NeedsManualReview.Should().BeFalse();
        dto.IsPossiblyStuck.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownOutcome_CanRetrySafely_False_NeedsManualReview_True()
    {
        var seeded = await SeedAsync(J3FulfillmentStatus.UnknownOutcome);
        await SetAdminAsync();
        var dto = await GetDetailAsync(seeded.Id);
        dto.CanRetrySafely.Should().BeFalse();
        dto.NeedsManualReview.Should().BeTrue();
        dto.IsPossiblyStuck.Should().BeFalse();
    }

    [Fact]
    public async Task Processing_Old_IsPossiblyStuck_True()
    {
        var seeded = await SeedAsync(
            J3FulfillmentStatus.Processing,
            updatedAtUtc: DateTime.UtcNow.AddMinutes(-20));
        await SetAdminAsync();
        var dto = await GetDetailAsync(seeded.Id);
        dto.IsPossiblyStuck.Should().BeTrue();
        dto.NeedsManualReview.Should().BeTrue();
        dto.CanRetrySafely.Should().BeFalse();
    }

    [Fact]
    public async Task Processing_Recent_IsPossiblyStuck_False()
    {
        var seeded = await SeedAsync(
            J3FulfillmentStatus.Processing,
            updatedAtUtc: DateTime.UtcNow.AddMinutes(-1));
        await SetAdminAsync();
        var dto = await GetDetailAsync(seeded.Id);
        dto.IsPossiblyStuck.Should().BeFalse();
        dto.NeedsManualReview.Should().BeFalse();
    }

    [Fact]
    public async Task Created_NeedsManualReview_False()
    {
        var seeded = await SeedAsync(
            J3FulfillmentStatus.Created,
            tracking: UniqueTracking(),
            j3OrderId: "j3-ok");
        await SetAdminAsync();
        var dto = await GetDetailAsync(seeded.Id);
        dto.NeedsManualReview.Should().BeFalse();
        dto.CanRetrySafely.Should().BeFalse();
        dto.IsPossiblyStuck.Should().BeFalse();
    }

    private async Task<PagedResult<J3FulfillmentAdminListItemDto>> ListAsync(string query)
    {
        var response = await _client.GetAsync($"/api/admin/j3-fulfillments?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PagedResult<J3FulfillmentAdminListItemDto>>(JsonOptions))!;
    }

    private async Task<J3FulfillmentAdminDetailDto> GetDetailAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/admin/j3-fulfillments/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<J3FulfillmentAdminDetailDto>(JsonOptions))!;
    }

    private static string UniqueTracking() => $"TRK{Guid.NewGuid():N}"[..16];

    private async Task<(Guid Id, Guid OrderId, string OrderNumber)> SeedAsync(
        string status,
        string? tracking = null,
        string? j3OrderId = null,
        string? j3OrderCode = null,
        string? deliveryPointId = null,
        string? lastErrorCode = null,
        DateTime? lastErrorAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "cliente@esotera.demo");
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"J3A{Guid.NewGuid():N}"[..12],
            UserId = user.Id,
            Status = OrderStatus.PaymentApproved,
            Subtotal = 50,
            Discount = 0,
            ShippingPrice = 12.99m,
            Total = 62.99m,
            ShippingMethodId = ShippingMethod.J3,
            ShippingMethodName = ShippingMethod.J3,
            ShippingProvider = "J3",
            ShipCep = "01310100",
            ShipStreet = "Av Paulista",
            ShipNumber = "1000",
            ShipNeighborhood = "Bela Vista",
            ShipCity = "São Paulo",
            ShipState = "SP",
            ShippingIsResidentialAddress = true,
            PaymentMethod = "pix",
            PaymentStatus = "approved",
            CustomerName = "Cliente Leak",
            CustomerEmail = "leak@esotera.test",
            CustomerPhone = "11988887777",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var fulfillment = new J3Fulfillment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = status,
            J3OrderId = j3OrderId,
            J3OrderCode = j3OrderCode,
            J3TrackingNumber = tracking,
            J3DeliveryPointId = deliveryPointId,
            AttemptCount = 1,
            LastErrorCode = lastErrorCode,
            LastErrorAtUtc = lastErrorAtUtc,
            CreatedAtUtc = now.AddMinutes(-30),
            UpdatedAtUtc = updatedAtUtc ?? now,
            CompletedAtUtc = status == J3FulfillmentStatus.Created ? now : null
        };
        db.Orders.Add(order);
        db.J3Fulfillments.Add(fulfillment);
        await db.SaveChangesAsync();
        return (fulfillment.Id, order.Id, order.OrderNumber);
    }
}
