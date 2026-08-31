namespace Esotera.Application.Interfaces;

/// <summary>
/// Cliente HTTP do Melhor Envio. Superfície deliberadamente mínima:
/// cotação (POST /me/shipment/calculate) e inserção no carrinho (POST /me/cart).
/// NÃO existe método de checkout/compra, generate ou print — a ausência é a garantia.
/// </summary>
public interface IMelhorEnvioShipmentClient
{
    Task<MelhorEnvioCalculateOutcome> CalculateAsync(
        MelhorEnvioCalculateRequest request,
        string accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Insere o frete no carrinho. Operação sem custo: não debita a carteira
    /// e não gera etiqueta. Exige o escopo cart-write.
    /// </summary>
    Task<MelhorEnvioCartOutcome> CreateCartItemAsync(
        MelhorEnvioCartRequest request,
        string accessToken,
        CancellationToken cancellationToken = default);
}

public sealed record MelhorEnvioCalculateRequest(
    string FromPostalCode,
    string ToPostalCode,
    decimal HeightCm,
    decimal WidthCm,
    decimal LengthCm,
    /// <summary>Peso em kg (ex.: 0.4 para 400 g).</summary>
    decimal WeightKg,
    string Services
);

/// <summary>Resultado interno — nunca expor ao cliente HTTP da loja.</summary>
public sealed class MelhorEnvioCalculateOutcome
{
    public bool Ok { get; init; }
    public bool Unauthenticated { get; init; }
    public bool TimedOut { get; init; }
    public bool NetworkError { get; init; }
    public IReadOnlyList<MelhorEnvioRawServiceQuote> Services { get; init; } =
        Array.Empty<MelhorEnvioRawServiceQuote>();
}

/// <summary>Campos relevantes da resposta ME (já parseados).</summary>
public sealed class MelhorEnvioRawServiceQuote
{
    public int? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public int? ServiceId { get; init; }
    public string? ServiceName { get; init; }
    public decimal? Price { get; init; }
    public decimal? CustomPrice { get; init; }
    public int? DeliveryTime { get; init; }
    public int? CustomDeliveryTime { get; init; }
    public string? Error { get; init; }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
}
