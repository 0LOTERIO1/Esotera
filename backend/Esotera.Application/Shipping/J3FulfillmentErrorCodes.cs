using System.Text;

namespace Esotera.Application.Shipping;

/// <summary>
/// Sanitização de LastErrorCode — nunca persistir PII, payload GraphQL, token ou exception.ToString().
/// </summary>
public static class J3FulfillmentErrorCodes
{
    public const int MaxLength = 64;

    public const string Http401 = "HTTP_401";
    public const string Http500 = "HTTP_500";
    public const string TimeoutUnknown = "TIMEOUT_UNKNOWN";
    public const string GraphqlValidation = "GRAPHQL_VALIDATION";
    public const string GraphqlAmbiguous = "GRAPHQL_AMBIGUOUS";
    public const string Configuration = "CONFIGURATION";
    public const string Unknown = "UNKNOWN";
    public const string J3Disabled = "J3_DISABLED";
    public const string FulfillmentDisabled = "FULFILLMENT_DISABLED";
    public const string MissingSellerId = "MISSING_SELLER_ID";
    public const string MissingSellerInformationId = "MISSING_SELLER_INFORMATION_ID";
    public const string ResidentialRequired = "RESIDENTIAL_REQUIRED";
    public const string InvalidCep = "INVALID_CEP";
    public const string MissingAddress = "MISSING_ADDRESS";
    public const string InvalidPackage = "INVALID_PACKAGE";
    public const string CreateRejected = "CREATE_REJECTED";
    /// <summary>HTTP 200 + success=false — sem prova de zero criação (Passo 4.2A).</summary>
    public const string SuccessFalse = "SUCCESS_FALSE";
    public const string SuccessWithoutOrderId = "SUCCESS_WITHOUT_ORDER_ID";
    public const string JsonInvalid = "JSON_INVALID";
    public const string NetworkUnknown = "NETWORK_UNKNOWN";
    public const string UnexpectedResultCount = "UNEXPECTED_RESULT_COUNT";
    public const string UnexpectedIndex = "UNEXPECTED_INDEX";

    /// <summary>
    /// Mantém apenas A-Z, 0-9 e underscore; trunca. Null/vazio → null.
    /// </summary>
    public static string? Sanitize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var sb = new StringBuilder(Math.Min(code.Length, MaxLength));
        foreach (var ch in code.Trim().ToUpperInvariant())
        {
            if (sb.Length >= MaxLength)
                break;
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_')
                sb.Append(ch);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    public static string FromHttpStatus(int statusCode) =>
        Sanitize($"HTTP_{statusCode}") ?? Configuration;
}
