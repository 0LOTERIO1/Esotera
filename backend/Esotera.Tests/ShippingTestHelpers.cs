using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esotera.Tests;

internal static class ShippingTestHelpers
{
    public static async Task EnableMelhorEnvioQuoteAsync(
        IServiceProvider rootServices,
        bool enabled = true,
        bool withOAuthConnection = true)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var settings = await db.StoreSettings.FirstAsync(s => s.Id == 1);
        settings.MelhorEnvioQuoteEnabled = enabled;
        settings.ShippingOriginCep = "08061420";
        settings.PackageLengthCm = 16m;
        settings.PackageWidthCm = 11m;
        settings.PackageHeightCm = 6m;
        settings.PackageWeightGrams = 400;
        await db.SaveChangesAsync();

        if (withOAuthConnection)
            await EnsureOAuthConnectionAsync(rootServices);
        else
            await ClearOAuthConnectionsAsync(rootServices);
    }

    public static async Task EnsureOAuthConnectionAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        var enc = scope.ServiceProvider.GetRequiredService<IIntegrationsEncryptionService>();

        if (await db.MelhorEnvioConnections.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        db.MelhorEnvioConnections.Add(new MelhorEnvioConnection
        {
            Id = Guid.NewGuid(),
            AccessTokenCipher = enc.Encrypt("fake-access-seed"),
            RefreshTokenCipher = enc.Encrypt("fake-refresh-seed"),
            AccessTokenExpiresAtUtc = now.AddDays(20),
            RefreshTokenExpiresAtUtc = now.AddDays(40),
            ConnectedAtUtc = now,
            UpdatedAtUtc = now,
            Scopes = "shipping-calculate",
            Environment = "sandbox"
        });
        await db.SaveChangesAsync();
    }

    public static async Task ClearOAuthConnectionsAsync(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        db.MelhorEnvioConnections.RemoveRange(db.MelhorEnvioConnections);
        await db.SaveChangesAsync();
    }

    public static FakeMelhorEnvioShipmentClient GetShipmentFake(IServiceProvider rootServices) =>
        rootServices.GetRequiredService<FakeMelhorEnvioShipmentClient>();

    public static FakeJ3Client GetJ3Fake(IServiceProvider rootServices) =>
        rootServices.GetRequiredService<FakeJ3Client>();

    public static object DefaultAdminSettingsPayload(
        bool melhorEnvioQuoteEnabled = false,
        decimal freeShippingMin = 99.90m,
        string[]? freeShippingStates = null,
        bool subsidyEnabled = false,
        decimal subsidyAmount = 10m,
        decimal j3Price = 12m,
        int j3Cutoff = 12) =>
        new
        {
            storeName = "Esotera",
            freeShippingMin,
            freeShippingStates = freeShippingStates ?? new[] { "SP", "RJ", "MG", "ES", "PR", "SC", "RS" },
            j3Price,
            j3CutoffHour = j3Cutoff,
            shippingSubsidyEnabled = subsidyEnabled,
            shippingSubsidyAmount = subsidyAmount,
            shippingOriginCep = "08061-420",
            packageLengthCm = 16m,
            packageWidthCm = 11m,
            packageHeightCm = 6m,
            packageWeightGrams = 400,
            melhorEnvioQuoteEnabled
        };
}
