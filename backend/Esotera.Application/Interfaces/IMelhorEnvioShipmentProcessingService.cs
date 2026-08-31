namespace Esotera.Application.Interfaces;

/// <summary>
/// Fase C1: cria o envio no CARRINHO do Melhor Envio.
/// Não compra frete, não gera etiqueta e não imprime etiqueta.
/// </summary>
public interface IMelhorEnvioShipmentProcessingService
{
    Task<MelhorEnvioCartCreationResult> CreateCartShipmentAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}

public sealed record MelhorEnvioCartCreationResult(
    bool Ok,
    /// <summary>Status final do MelhorEnvioShipment.</summary>
    string? Status,
    string? ShipmentId,
    string? Protocol,
    string? ErrorCode,
    string? ErrorMessage,
    /// <summary>Já existia envio no carrinho — nada foi criado de novo.</summary>
    bool AlreadyCreated = false,
    /// <summary>Bloqueio local: nenhuma chamada foi feita ao Melhor Envio.</summary>
    bool BlockedLocally = false)
{
    public static MelhorEnvioCartCreationResult Blocked(string code, string message) =>
        new(false, null, null, null, code, message, BlockedLocally: true);
}
