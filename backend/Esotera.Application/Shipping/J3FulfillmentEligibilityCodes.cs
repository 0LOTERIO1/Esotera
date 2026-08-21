namespace Esotera.Application.Shipping;

/// <summary>
/// Motivos de elegibilidade J3 (gate local). Sem PII. Independente de HTTP/GraphQL.
/// </summary>
public static class J3FulfillmentEligibilityCodes
{
    public const string Eligible = "Eligible";
    public const string FeatureDisabled = "FeatureDisabled";
    public const string WrongShippingMethod = "WrongShippingMethod";
    public const string PaymentNotApproved = "PaymentNotApproved";
    public const string MissingFiscalInvoice = "MissingFiscalInvoice";
    public const string FiscalInvoiceNotAuthorized = "FiscalInvoiceNotAuthorized";
    public const string MissingNfeKey = "MissingNfeKey";
    public const string InvalidNfeKey = "InvalidNfeKey";
    public const string IncompleteShippingAddress = "IncompleteShippingAddress";
    public const string MissingResidentialFlag = "MissingResidentialFlag";
    public const string MissingCustomerName = "MissingCustomerName";
    public const string FulfillmentAlreadyExists = "FulfillmentAlreadyExists";
    public const string FulfillmentAlreadyCreated = "FulfillmentAlreadyCreated";
    public const string UnknownOutcomeRequiresReview = "UnknownOutcomeRequiresReview";
    public const string RetryableFailureNotAutoRetried = "RetryableFailureNotAutoRetried";
    public const string OrderNotFound = "OrderNotFound";
}
