using Esotera.Application.Common;
using Esotera.Application.DTOs.Coupons;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Esotera.Infrastructure.Services;

public class CouponService : ICouponService
{
    private readonly EsoteraDbContext _context;

    public CouponService(EsoteraDbContext context)
    {
        _context = context;
    }

    public async Task<CouponValidationResponse> ValidateAsync(Guid userId, string code, decimal subtotal)
    {
        try
        {
            var coupon = await FindByNormalizedCodeAsync(CouponCodeNormalizer.Normalize(code));
            await EnsureCouponUsableAsync(coupon, userId, subtotal, forUpdate: false);
            return new CouponValidationResponse(true, coupon.Code, coupon.DiscountAmount, null);
        }
        catch (ValidationException ex)
        {
            var msg = ex.Errors.Values.SelectMany(v => v).FirstOrDefault() ?? ex.Message;
            return new CouponValidationResponse(false, null, 0, msg);
        }
        catch (ConflictException ex)
        {
            return new CouponValidationResponse(false, null, 0, ex.Message);
        }
        catch (NotFoundException)
        {
            return new CouponValidationResponse(false, null, 0, "Cupom não encontrado.");
        }
    }

    public async Task<Coupon> LockAndValidateForOrderAsync(Guid userId, string code, decimal subtotal)
    {
        var normalized = CouponCodeNormalizer.Normalize(code);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("couponCode", "Cupom inválido.");

        Coupon coupon;
        if (_context.Database.IsRelational())
        {
            coupon = await _context.Coupons
                .FromSqlInterpolated($@"SELECT * FROM ""Coupons"" WHERE ""Code"" = {normalized} FOR UPDATE")
                .AsTracking()
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Cupom", normalized);
        }
        else
        {
            coupon = await FindByNormalizedCodeAsync(normalized);
        }

        await EnsureCouponUsableAsync(coupon, userId, subtotal, forUpdate: true);
        return coupon;
    }

    public async Task<IReadOnlyList<AdminCouponDto>> AdminListAsync(bool? isArchived = null, bool? isActive = null)
    {
        var query = _context.Coupons.AsNoTracking().AsQueryable();
        if (isArchived.HasValue)
            query = query.Where(c => c.IsArchived == isArchived.Value);
        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var list = await query.OrderByDescending(c => c.UpdatedAtUtc).ToListAsync();
        var counts = await _context.CouponUsages
            .GroupBy(u => u.CouponId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return list.Select(c => MapAdmin(c, counts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<AdminCouponDto?> AdminGetAsync(Guid id)
    {
        var coupon = await _context.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null) return null;
        var count = await _context.CouponUsages.CountAsync(u => u.CouponId == id);
        return MapAdmin(coupon, count);
    }

    public async Task<AdminCouponDto> AdminCreateAsync(CreateCouponRequest request)
    {
        var code = CouponCodeNormalizer.Normalize(request.Code);
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("code", "Código é obrigatório.");

        if (await _context.Coupons.AnyAsync(c => c.Code == code))
            throw new ConflictException($"Já existe um cupom com o código '{code}'.");

        var now = DateTime.UtcNow;
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(),
            Code = code,
            DiscountAmount = request.DiscountAmount,
            MinPurchase = request.MinPurchase,
            AppliesToShipping = false,
            OneUsePerCustomer = request.OneUsePerCustomer,
            MaxTotalUses = request.MaxTotalUses,
            IsActive = request.IsActive,
            IsArchived = false,
            ValidFromUtc = request.ValidFromUtc,
            ValidUntilUtc = request.ValidUntilUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();
        return MapAdmin(coupon, 0);
    }

    public async Task<AdminCouponDto> AdminUpdateAsync(Guid id, UpdateCouponRequest request)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Cupom", id);

        if (request.Code != null)
        {
            var code = CouponCodeNormalizer.Normalize(request.Code);
            if (string.IsNullOrWhiteSpace(code))
                throw new ValidationException("code", "Código inválido.");
            if (code != coupon.Code && await _context.Coupons.AnyAsync(c => c.Code == code && c.Id != id))
                throw new ConflictException($"Já existe um cupom com o código '{code}'.");
            coupon.Code = code;
        }

        if (request.DiscountAmount.HasValue) coupon.DiscountAmount = request.DiscountAmount.Value;
        if (request.MinPurchase.HasValue) coupon.MinPurchase = request.MinPurchase.Value;
        if (request.OneUsePerCustomer.HasValue) coupon.OneUsePerCustomer = request.OneUsePerCustomer.Value;
        if (request.ClearMaxTotalUses == true) coupon.MaxTotalUses = null;
        else if (request.MaxTotalUses.HasValue) coupon.MaxTotalUses = request.MaxTotalUses;
        if (request.IsActive.HasValue) coupon.IsActive = request.IsActive.Value;
        if (request.ClearValidFrom == true) coupon.ValidFromUtc = null;
        else if (request.ValidFromUtc.HasValue) coupon.ValidFromUtc = request.ValidFromUtc;
        if (request.ClearValidUntil == true) coupon.ValidUntilUtc = null;
        else if (request.ValidUntilUtc.HasValue) coupon.ValidUntilUtc = request.ValidUntilUtc;

        coupon.AppliesToShipping = false;
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var count = await _context.CouponUsages.CountAsync(u => u.CouponId == id);
        return MapAdmin(coupon, count);
    }

    public async Task<AdminCouponDto> AdminSetActiveAsync(Guid id, bool isActive)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Cupom", id);
        if (coupon.IsArchived && isActive)
            throw new ValidationException("isActive", "Restaure o cupom arquivado antes de ativá-lo.");
        coupon.IsActive = isActive;
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var count = await _context.CouponUsages.CountAsync(u => u.CouponId == id);
        return MapAdmin(coupon, count);
    }

    public async Task<AdminCouponDto> AdminArchiveAsync(Guid id)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Cupom", id);
        coupon.IsArchived = true;
        coupon.ArchivedAtUtc = DateTime.UtcNow;
        coupon.IsActive = false;
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var count = await _context.CouponUsages.CountAsync(u => u.CouponId == id);
        return MapAdmin(coupon, count);
    }

    public async Task<AdminCouponDto> AdminRestoreAsync(Guid id)
    {
        var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Cupom", id);
        coupon.IsArchived = false;
        coupon.ArchivedAtUtc = null;
        // Não reativa automaticamente
        coupon.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var count = await _context.CouponUsages.CountAsync(u => u.CouponId == id);
        return MapAdmin(coupon, count);
    }

    private async Task<Coupon> FindByNormalizedCodeAsync(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            throw new NotFoundException("Cupom", normalized);

        return await _context.Coupons.FirstOrDefaultAsync(c => c.Code == normalized)
            ?? throw new NotFoundException("Cupom", normalized);
    }

    private async Task EnsureCouponUsableAsync(Coupon coupon, Guid userId, decimal subtotal, bool forUpdate)
    {
        if (coupon.IsArchived)
            throw new ValidationException("couponCode", "Cupom não está disponível.");

        if (!coupon.IsActive)
            throw new ValidationException("couponCode", "Cupom inativo.");

        var now = DateTime.UtcNow;
        if (coupon.ValidFromUtc.HasValue && now < coupon.ValidFromUtc)
            throw new ValidationException("couponCode", "Cupom ainda não é válido.");

        if (coupon.ValidUntilUtc.HasValue && now > coupon.ValidUntilUtc)
            throw new ValidationException("couponCode", "Cupom expirado.");

        if (subtotal < coupon.MinPurchase)
            throw new ValidationException(
                "couponCode",
                $"Compra mínima de R$ {coupon.MinPurchase:N2} para este cupom.");

        if (coupon.OneUsePerCustomer)
        {
            var alreadyUsed = await _context.CouponUsages
                .AnyAsync(cu => cu.CouponId == coupon.Id && cu.UserId == userId);
            if (alreadyUsed)
                throw new ConflictException("Você já utilizou este cupom.");
        }

        if (coupon.MaxTotalUses.HasValue)
        {
            var usageCount = await _context.CouponUsages.CountAsync(cu => cu.CouponId == coupon.Id);
            if (usageCount >= coupon.MaxTotalUses.Value)
            {
                throw forUpdate
                    ? new ConflictException("Este cupom esgotou o limite de utilizações.")
                    : new ValidationException("couponCode", "Este cupom esgotou o limite de utilizações.");
            }
        }
    }

    private static AdminCouponDto MapAdmin(Coupon c, int usageCount) =>
        new(
            c.Id,
            c.Code,
            c.DiscountAmount,
            c.MinPurchase,
            c.AppliesToShipping,
            c.OneUsePerCustomer,
            c.MaxTotalUses,
            usageCount,
            c.IsActive,
            c.IsArchived,
            c.ArchivedAtUtc,
            c.ValidFromUtc,
            c.ValidUntilUtc,
            c.CreatedAtUtc,
            c.UpdatedAtUtc
        );
}
