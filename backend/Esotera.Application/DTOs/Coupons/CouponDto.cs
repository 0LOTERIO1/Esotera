namespace Esotera.Application.DTOs.Coupons;

public record CouponValidationRequest(string Code, decimal Subtotal);

public record CouponValidationResponse(
    bool IsValid,
    string? Code,
    decimal DiscountAmount,
    string? ErrorMessage
);

public record AdminCouponDto(
    Guid Id,
    string Code,
    decimal DiscountAmount,
    decimal MinPurchase,
    bool AppliesToShipping,
    bool OneUsePerCustomer,
    int? MaxTotalUses,
    int UsageCount,
    bool IsActive,
    bool IsArchived,
    DateTime? ArchivedAtUtc,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateCouponRequest(
    string Code,
    decimal DiscountAmount,
    decimal MinPurchase,
    bool OneUsePerCustomer = true,
    int? MaxTotalUses = null,
    bool IsActive = true,
    DateTime? ValidFromUtc = null,
    DateTime? ValidUntilUtc = null
);

public record UpdateCouponRequest(
    string? Code,
    decimal? DiscountAmount,
    decimal? MinPurchase,
    bool? OneUsePerCustomer,
    int? MaxTotalUses,
    bool? ClearMaxTotalUses,
    bool? IsActive,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    bool? ClearValidFrom,
    bool? ClearValidUntil
);
