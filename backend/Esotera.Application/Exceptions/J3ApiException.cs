namespace Esotera.Application.Exceptions;

/// <summary>
/// Falha sanitizada da API GraphQL J3. Nunca inclui token, Authorization, headers completos,
/// body integral ou PII.
/// </summary>
public sealed class J3ApiException : Exception
{
    public string OperationName { get; }
    public int? HttpStatus { get; }
    public IReadOnlyList<string>? GraphQlErrorCodes { get; }

    public J3ApiException(
        string operationName,
        string sanitizedMessage,
        int? httpStatus = null,
        IEnumerable<string>? graphQlErrorCodes = null,
        Exception? innerException = null)
        : base(sanitizedMessage, innerException)
    {
        OperationName = operationName;
        HttpStatus = httpStatus;
        GraphQlErrorCodes = graphQlErrorCodes?.ToArray();
    }
}
