namespace Esotera.Application.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Acesso negado.") : base(message) { }
}
