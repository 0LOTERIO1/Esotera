namespace Esotera.Application.DTOs.Settings;

public record PublicStoreSettingsDto(
    string StoreName,
    decimal FreeShippingMin,
    string[] FreeShippingStates,
    decimal J3Price,
    int J3CutoffHour,
    bool ShippingSubsidyEnabled,
    decimal ShippingSubsidyAmount
);

public record AdminStoreSettingsDto(
    string StoreName,
    decimal FreeShippingMin,
    string[] FreeShippingStates,
    decimal J3Price,
    int J3CutoffHour,
    bool ShippingSubsidyEnabled,
    decimal ShippingSubsidyAmount,
    DateTime UpdatedAtUtc
);

public record UpdateStoreSettingsRequest(
    string StoreName,
    decimal FreeShippingMin,
    string[] FreeShippingStates,
    decimal J3Price,
    int J3CutoffHour,
    bool ShippingSubsidyEnabled,
    decimal ShippingSubsidyAmount
);
