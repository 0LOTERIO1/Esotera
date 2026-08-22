namespace Esotera.Application.Shipping;

/// <summary>Códigos sanitizados do sync manual de tracking/status J3 (read-only remoto).</summary>
public static class J3TrackingSyncErrorCodes
{
    public const string NotEligible = "TRACKING_SYNC_NOT_ELIGIBLE";
    /// <summary>J3OrderCode e J3TrackingNumber locais preenchidos e divergentes — inconsistência local, não erro remoto.</summary>
    public const string LocalCodeMismatch = "TRACKING_SYNC_LOCAL_CODE_MISMATCH";
    public const string LookupFailed = "TRACKING_SYNC_LOOKUP_FAILED";
    public const string NotFound = "TRACKING_SYNC_NOT_FOUND";
    public const string IdMismatch = "TRACKING_SYNC_ID_MISMATCH";
    public const string TrackingMismatch = "TRACKING_SYNC_TRACKING_MISMATCH";
    public const string ZipMismatch = "TRACKING_SYNC_ZIP_MISMATCH";
    public const string StatusMissing = "TRACKING_SYNC_STATUS_MISSING";
    public const string DeliveryPointMissing = "TRACKING_SYNC_DELIVERY_POINT_MISSING";
    public const string Ambiguous = "TRACKING_SYNC_AMBIGUOUS";
    public const string MissingRemoteId = "TRACKING_SYNC_MISSING_REMOTE_ID";
}
