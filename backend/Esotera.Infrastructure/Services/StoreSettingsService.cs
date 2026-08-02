using Esotera.Application.Common;
using Esotera.Application.DTOs.Settings;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class StoreSettingsService : IStoreSettingsService
{
    private readonly EsoteraDbContext _context;

    public StoreSettingsService(EsoteraDbContext context)
    {
        _context = context;
    }

    public async Task<PublicStoreSettingsDto> GetPublicAsync()
    {
        var s = await GetOrCreateAsync();
        return MapPublic(s);
    }

    public async Task<AdminStoreSettingsDto> GetAdminAsync()
    {
        var s = await GetOrCreateAsync();
        return MapAdmin(s);
    }

    public async Task<AdminStoreSettingsDto> UpdateAsync(UpdateStoreSettingsRequest request)
    {
        if (!FreeShippingStatesParser.TryValidate(request.FreeShippingStates, out var error, out var csv))
            throw new Application.Exceptions.ValidationException("freeShippingStates", error!);

        var originDigits = BrazilianCep.TryNormalize(request.ShippingOriginCep)
            ?? throw new Application.Exceptions.ValidationException("shippingOriginCep", "CEP de origem inválido.");

        var s = await GetOrCreateAsync(tracking: true);
        s.StoreName = request.StoreName.Trim();
        s.FreeShippingMin = request.FreeShippingMin;
        s.FreeShippingStatesCsv = csv;
        s.J3Price = request.J3Price;
        s.J3CutoffHour = request.J3CutoffHour;
        s.ShippingSubsidyEnabled = request.ShippingSubsidyEnabled;
        s.ShippingSubsidyAmount = request.ShippingSubsidyAmount;
        s.ShippingOriginCep = originDigits;
        s.PackageLengthCm = request.PackageLengthCm;
        s.PackageWidthCm = request.PackageWidthCm;
        s.PackageHeightCm = request.PackageHeightCm;
        s.PackageWeightGrams = request.PackageWeightGrams;
        s.MelhorEnvioQuoteEnabled = request.MelhorEnvioQuoteEnabled;
        s.UpdatedAtUtc = DateTime.UtcNow;
        // Campos legados CouponDiscount / CouponMinPurchase não são atualizados pela API
        await _context.SaveChangesAsync();
        return MapAdmin(s);
    }

    private async Task<StoreSettings> GetOrCreateAsync(bool tracking = false)
    {
        var query = tracking ? _context.StoreSettings.AsQueryable() : _context.StoreSettings.AsNoTracking();
        var existing = await query.FirstOrDefaultAsync(x => x.Id == 1);
        if (existing != null)
            return existing;

        var created = CreateDefault();
        _context.StoreSettings.Add(created);
        await _context.SaveChangesAsync();
        return created;
    }

    public static StoreSettings CreateDefault() => new()
    {
        Id = 1,
        StoreName = "Esotera",
        FreeShippingMin = 99.90m,
        FreeShippingStatesCsv = "SP,RJ,MG,ES,PR,SC,RS",
        J3Price = 12.00m,
        J3CutoffHour = 12,
#pragma warning disable CS0618
        CouponDiscount = 5.00m,
        CouponMinPurchase = 30.00m,
#pragma warning restore CS0618
        ShippingSubsidyEnabled = false,
        ShippingSubsidyAmount = 10.00m,
        ShippingOriginCep = "08061420",
        PackageLengthCm = 16m,
        PackageWidthCm = 11m,
        PackageHeightCm = 6m,
        PackageWeightGrams = 400,
        MelhorEnvioQuoteEnabled = false,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static PublicStoreSettingsDto MapPublic(StoreSettings s) =>
        new(
            s.StoreName,
            s.FreeShippingMin,
            FreeShippingStatesParser.Parse(s.FreeShippingStatesCsv).ToArray(),
            s.J3Price,
            s.J3CutoffHour,
            s.ShippingSubsidyEnabled,
            s.ShippingSubsidyAmount
        );

    private static AdminStoreSettingsDto MapAdmin(StoreSettings s) =>
        new(
            s.StoreName,
            s.FreeShippingMin,
            FreeShippingStatesParser.Parse(s.FreeShippingStatesCsv).ToArray(),
            s.J3Price,
            s.J3CutoffHour,
            s.ShippingSubsidyEnabled,
            s.ShippingSubsidyAmount,
            BrazilianCep.FormatMasked(BrazilianCep.TryNormalize(s.ShippingOriginCep) ?? "08061420"),
            s.PackageLengthCm,
            s.PackageWidthCm,
            s.PackageHeightCm,
            s.PackageWeightGrams,
            s.MelhorEnvioQuoteEnabled,
            s.UpdatedAtUtc
        );
}
