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
    string ShippingOriginCep,
    decimal PackageLengthCm,
    decimal PackageWidthCm,
    decimal PackageHeightCm,
    int PackageWeightGrams,
    bool MelhorEnvioQuoteEnabled,
    DateTime UpdatedAtUtc
);

public record UpdateStoreSettingsRequest(
    string StoreName,
    decimal FreeShippingMin,
    string[] FreeShippingStates,
    decimal J3Price,
    int J3CutoffHour,
    bool ShippingSubsidyEnabled,
    decimal ShippingSubsidyAmount,
    string ShippingOriginCep,
    decimal PackageLengthCm,
    decimal PackageWidthCm,
    decimal PackageHeightCm,
    int PackageWeightGrams,
    bool MelhorEnvioQuoteEnabled
);
