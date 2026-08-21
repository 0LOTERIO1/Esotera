namespace Esotera.Application.Shipping;

/// <summary>
/// Query read-only comprovada em produção: searchOrderByCode.
/// Selection set EXATO — sem campos especulativos.
/// </summary>
public static class J3SearchOrderByCodeQuery
{
    public const string OperationName = "SearchJ3OrderByCode";

    public const string Document =
        """
        query SearchJ3OrderByCode($code: String!) {
          searchOrderByCode(code: $code) {
            id
            date
            nf
            status
            storeName
            ecommerce
            deliveryPoints {
              addressName
              addressZipCode
              trackingNumber
            }
          }
        }
        """;
}
