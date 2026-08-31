namespace Esotera.Domain.Entities;

/// <summary>
/// Ciclo logístico Melhor Envio, 1:1 com Order. Registro LOCAL: a existência da linha é a
/// obrigação de despachar, independente de a API do Melhor Envio já ter sido chamada.
/// Dimensões/peso do pacote ficam em StoreSettings (não duplicar aqui).
/// Nunca guardar token, payload bruto ou XML da NF-e.
/// </summary>
public class MelhorEnvioShipment
{
    public Guid Id { get; set; }

    /// <summary>Pedido Esotera — UNIQUE (um envio por pedido).</summary>
    public Guid OrderId { get; set; }

    /// <summary>Ambiente em que este envio deve ser criado (sandbox/production).</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary><see cref="Enums.MelhorEnvioShipmentStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Snapshot da cotação — id do serviço no Melhor Envio (ex.: 2 = SEDEX).</summary>
    public int? ServiceId { get; set; }

    /// <summary>Snapshot da cotação — serviço (ex.: SEDEX, PAC).</summary>
    public string? ServiceName { get; set; }

    /// <summary>Snapshot da cotação — transportadora (ex.: Correios).</summary>
    public string? CarrierName { get; set; }

    /// <summary>Rótulo mostrado ao cliente no checkout (ex.: "Melhor Envio - Expresso").</summary>
    public string? SelectedDisplayName { get; set; }

    /// <summary>Preço cotado na transportadora no momento do pedido.</summary>
    public decimal? QuotedPrice { get; set; }

    /// <summary>Frete efetivamente cobrado do cliente. Snapshot — a fonte é Order.ShippingPrice.</summary>
    public decimal? ChargedFreightPrice { get; set; }

    /// <summary>Prazo em dias úteis da cotação; null = desconhecido.</summary>
    public int? DeliveryTimeDays { get; set; }

    /// <summary>ID do envio no Melhor Envio. Unique filtrado quando preenchido.</summary>
    public string? MelhorEnvioShipmentId { get; set; }

    /// <summary>Protocolo do Melhor Envio (ex.: ORD-20220397305).</summary>
    public string? MelhorEnvioProtocol { get; set; }

    public string? TrackingCode { get; set; }
    public string? TrackingUrl { get; set; }
    public string? LabelUrl { get; set; }

    /// <summary>UTC da inserção no carrinho do Melhor Envio.</summary>
    public DateTime? CartCreatedAtUtc { get; set; }

    /// <summary>UTC da compra da etiqueta (saldo debitado).</summary>
    public DateTime? PurchasedAtUtc { get; set; }

    /// <summary>UTC da geração da etiqueta.</summary>
    public DateTime? LabelGeneratedAtUtc { get; set; }

    /// <summary>UTC da última sincronização bem-sucedida com o Melhor Envio.</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    /// <summary>Código curto sanitizado (ex.: HTTP_401). Sem PII/payload/token.</summary>
    public string? LastSyncErrorCode { get; set; }

    /// <summary>Mensagem operacional para o Admin. Nunca segredo nem payload bruto.</summary>
    public string? LastSyncErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
