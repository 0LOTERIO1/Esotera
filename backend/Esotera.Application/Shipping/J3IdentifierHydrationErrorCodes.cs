namespace Esotera.Application.Shipping;

/// <summary>Códigos sanitizados da hidratação manual de J3OrderCode/J3TrackingNumber.</summary>
public static class J3IdentifierHydrationErrorCodes
{
    public const string NotEligible = "J3_IDENTIFIER_HYDRATION_NOT_ELIGIBLE";
    /// <summary>Code/tracking locais divergentes ou só um preenchido — inconsistência local.</summary>
    public const string LocalConflict = "J3_IDENTIFIER_HYDRATION_LOCAL_CONFLICT";
    public const string LookupFailed = "J3_IDENTIFIER_HYDRATION_LOOKUP_FAILED";
    public const string NotFound = "J3_IDENTIFIER_HYDRATION_NOT_FOUND";
    public const string TrackingMissing = "J3_IDENTIFIER_HYDRATION_TRACKING_MISSING";
    public const string IdMismatch = "J3_IDENTIFIER_HYDRATION_ID_MISMATCH";
    public const string ZipMismatch = "J3_IDENTIFIER_HYDRATION_ZIP_MISMATCH";
    public const string DeliveryPointMissing = "J3_IDENTIFIER_HYDRATION_DELIVERY_POINT_MISSING";
}
