namespace Esotera.Application.Exceptions;

public class UnauthorizedAppException : Exception
{
    public UnauthorizedAppException(string message = "Não autorizado.") : base(message) { }
}
