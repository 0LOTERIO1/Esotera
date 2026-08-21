namespace Esotera.Application.DTOs.J3;

/// <summary>Confirmação explícita do recovery admin importOrderByAccessKey.</summary>
public sealed record J3ImportByAccessKeyConfirmRequest(string ConfirmOrderNumber);

/// <summary>
/// Resposta sanitizada do recovery. Sem token, XML, ChNFe, CPF, telefone ou endereço.
/// Fulfillment nunca é promovido a Created neste endpoint.
/// </summary>
public sealed record J3ImportByAccessKeyAdminResultDto(
    Guid OrderId,
    string? OrderNumber,
    Guid? FulfillmentId,
    string FulfillmentStatus,
    string? FulfillmentLastErrorCode,
    bool FulfillmentUnchanged,
    string Outcome,
    string? ErrorCode,
    bool HttpSent,
    string OperationName);
