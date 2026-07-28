namespace Esotera.Application.Interfaces;

/// <summary>
/// Abstração de tempo para regras de frete (J3) e testes determinísticos.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
