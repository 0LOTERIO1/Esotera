namespace Esotera.Domain.Enums;

/// <summary>
/// Status do fulfillment J3 (persistido como string, padrão Order.Status).
/// UnknownOutcome NUNCA pode sofrer retry automático — exige reconciliação manual/operacional.
/// </summary>
public static class J3FulfillmentStatus
{
    /// <summary>Ainda não tentou createTmsOrder.</summary>
    public const string Pending = "pending";

    /// <summary>
    /// Claim obtido. HTTP futuro só depois do claim persistido.
    /// Erro local ANTES de SendAsync → futuro RetryableFailure pode ser seguro.
    /// </summary>
    public const string Processing = "processing";

    /// <summary>Success inequívoco: success=true AND orderId não vazio.</summary>
    public const string Created = "created";

    /// <summary>
    /// Entrega comprovadamente NÃO criada (validação local pré-HTTP, ou parse/validation GraphQL pré-resolver).
    /// Retry futuro pode ser permitido. Não usar para 401/403/success=false sem contrato.
    /// </summary>
    public const string RetryableFailure = "retryable_failure";

    /// <summary>
    /// SendAsync começou e não há Success inequívoco nem prova de pré-execução.
    /// NUNCA volta automaticamente para Pending. NUNCA auto-retry. Exige reconciliação/revisão manual.
    /// </summary>
    public const string UnknownOutcome = "unknown_outcome";

    public static readonly string[] All =
    [
        Pending,
        Processing,
        Created,
        RetryableFailure,
        UnknownOutcome
    ];

    public static bool IsValid(string status) => All.Contains(status);
}
