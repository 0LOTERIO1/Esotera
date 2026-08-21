namespace Esotera.Application.Shipping;

/// <summary>
/// Mutation GraphQL importOrderByAccessKey (NF-e → TMS via chave de acesso).
/// Separada de createTmsOrders. Sem fallback automático.
/// ApiResult (introspecção): success Boolean!, message String, error ApiError.
/// ApiError: clientId String, description String, errorCode Int!, layer ErrorLayer!.
/// </summary>
public static class J3ImportOrderByAccessKeyMutation
{
    public const string OperationName = "ImportJ3OrderByAccessKey";

    public const string Document =
        """
        mutation ImportJ3OrderByAccessKey($input: ImportOrderByAccessKeyInput!) {
          importOrderByAccessKey(input: $input) {
            success
            message
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
