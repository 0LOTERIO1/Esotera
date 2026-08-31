namespace Esotera.Domain.Enums;

/// <summary>
/// Status do ciclo logístico Melhor Envio (persistido como string, padrão Order.Status).
/// Fase B usa apenas <see cref="WaitingInvoice"/> e <see cref="ReadyToCreate"/> — os demais
/// existem para o processador futuro e nunca são atribuídos sem chamada real à API.
/// </summary>
public static class MelhorEnvioShipmentStatus
{
    /// <summary>Pago, mas sem NF-e autorizada. Envio comercial exige a chave da nota.</summary>
    public const string WaitingInvoice = "waiting_invoice";

    /// <summary>NF-e autorizada e dados suficientes: apto a criar o envio no Melhor Envio.</summary>
    public const string ReadyToCreate = "ready_to_create";

    /// <summary>Claim obtido para inserir no carrinho. HTTP só depois do claim persistido.</summary>
    public const string CartPending = "cart_pending";

    /// <summary>Frete inserido no carrinho. Nada foi pago ainda.</summary>
    public const string CartCreated = "cart_created";

    /// <summary>Claim obtido para checkout. Debita a carteira — jamais sem confirmação explícita.</summary>
    public const string PurchasePending = "purchase_pending";

    /// <summary>Etiqueta paga (saldo debitado).</summary>
    public const string Purchased = "purchased";

    /// <summary>Etiqueta gerada e imprimível.</summary>
    public const string LabelGenerated = "label_generated";

    /// <summary>
    /// Falha registrada com código sanitizado. Não sofre retry automático:
    /// reprocessamento é sempre ação explícita do Admin.
    /// </summary>
    public const string Failed = "failed";

    /// <summary>Envio cancelado (no Melhor Envio ou localmente antes de existir remotamente).</summary>
    public const string Cancelled = "cancelled";

    public static readonly string[] All =
    [
        WaitingInvoice,
        ReadyToCreate,
        CartPending,
        CartCreated,
        PurchasePending,
        Purchased,
        LabelGenerated,
        Failed,
        Cancelled
    ];

    public static bool IsValid(string status) => All.Contains(status);
}
