namespace Esotera.Domain.Entities;

/// <summary>
/// Fulfillment J3 1:1 com Order. Processo externo assíncrono/faturável.
/// Sem body GraphQL completo. Package dims/weight ficam em StoreSettings (não duplicar aqui).
/// </summary>
public class J3Fulfillment
{
    public Guid Id { get; set; }

    /// <summary>Pedido Esotera — UNIQUE (um fulfillment por pedido).</summary>
    public Guid OrderId { get; set; }

    /// <summary><see cref="Enums.J3FulfillmentStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>ID da ordem na J3. Nullable; unique filtrado quando preenchido.</summary>
    public string? J3OrderId { get; set; }

    public string? J3OrderCode { get; set; }
    public string? J3TrackingNumber { get; set; }
    public string? J3DeliveryPointId { get; set; }

    /// <summary>URL da etiqueta. Nullable — generateOrderStamp ainda não existe neste passo.</summary>
    public string? J3StampUrl { get; set; }

    public int AttemptCount { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastErrorAtUtc { get; set; }

    /// <summary>Código curto sanitizado (ex.: HTTP_500, TIMEOUT_UNKNOWN). Sem PII/payload/token.</summary>
    public string? LastErrorCode { get; set; }

    /// <summary>
    /// Status logístico RAW da J3 (searchOrderByCode.status). Nunca confundir com <see cref="Status"/> de integração.
    /// </summary>
    public string? J3RemoteStatus { get; set; }

    /// <summary>UTC da última sincronização de status bem-sucedida.</summary>
    public DateTime? J3LastStatusSyncAtUtc { get; set; }

    /// <summary>Erro sanitizado da última tentativa de sync de status. Null após sucesso.</summary>
    public string? J3LastStatusSyncErrorCode { get; set; }

    /// <summary>UTC da última tentativa de sync que falhou. Null após sucesso.</summary>
    public DateTime? J3LastStatusSyncErrorAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Order Order { get; set; } = null!;
}
