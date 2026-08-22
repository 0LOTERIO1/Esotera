namespace Esotera.Application.Shipping;

/// <summary>
/// Query read-only comprovada por introspecção: getOrderDetails(orderId).
/// Selection set mínimo — sem campos especulativos.
/// </summary>
public static class J3GetOrderDetailsQuery
{
    public const string OperationName = "GetJ3OrderDetails";

    public const string Document =
        """
        query GetJ3OrderDetails($orderId: String!) {
          getOrderDetails(orderId: $orderId) {
            id
            status
            deliveryPoint {
              id
              trackingNumber
              addressZipCode
              addressName
            }
          }
        }
        """;
}
