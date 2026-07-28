namespace Esotera.Application.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) 
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error) 
        : base(error)
    {
        Errors = new Dictionary<string, string[]> { { field, new[] { error } } };
    }
}
