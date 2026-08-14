namespace Esotera.Application.Shipping;

/// <summary>
/// Mutation GraphQL createTmsOrders (Pedido Avulso / portal oficial).
/// Esotera envia exatamente 1 CreateTmsOrderInput. ApiError: layer, clientId, errorCode, description.
/// </summary>
public static class J3CreateTmsOrderMutation
{
    public const string OperationName = "CreateJ3TmsOrders";

    public const string Document =
        """
        mutation CreateJ3TmsOrders($inputs: [CreateTmsOrderInput!]!) {
          createTmsOrders(inputs: $inputs) {
            orderId
            success
            message
            index
            errorField
            error {
              layer
              clientId
              errorCode
              description
            }
          }
        }
        """;
}
