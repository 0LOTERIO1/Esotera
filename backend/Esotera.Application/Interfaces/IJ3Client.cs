using Esotera.Application.Exceptions;

namespace Esotera.Application.Interfaces;

/// <summary>
/// Cliente GraphQL J3 Flex — somente leitura (cobertura + tracking).
/// Coverage ligada ao quote/CreateOrder (Passo 3). Tracking ainda sem UI.
/// </summary>
/// <remarks>
/// Preferência de assinatura simples: <see cref="IsServiceAreaAsync"/> retorna bool apenas para
/// resposta legítima true/false da API. Falhas operacionais (HTTP, GraphQL errors, timeout,
/// JSON inválido, config ausente, CEP inválido) → <see cref="J3ApiException"/> — nunca false por erro.
/// <see cref="GetTrackingAsync"/> retorna null quando a API responde sem objeto (não encontrado);
/// falhas → <see cref="J3ApiException"/>.
/// </remarks>
public interface IJ3Client
{
    /// <summary>
    /// Verifica cobertura por CEP (query isValidServiceArea).
    /// Normaliza para 8 dígitos e envia no payload como #####-###. CEP inválido → exceção sem HTTP.
    /// </summary>
    Task<bool> IsServiceAreaAsync(string zipCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rastreamento (query getTrackingOrderSeller). Status permanece RAW da J3.
    /// null = resposta válida sem pedido (não encontrado).
    /// </summary>
    Task<J3TrackingResult?> GetTrackingAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>DTO de tracking J3 — datas opcionais nullable; Status RAW.</summary>
public sealed class J3TrackingResult
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    /// <summary>Status bruto da J3 — sem tradução nesta fase.</summary>
    public string? Status { get; init; }
    public string? Ecommerce { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? CollectedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? CanceledAt { get; init; }
}
