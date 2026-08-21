using Esotera.Application.Common;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;

namespace Esotera.Application.Shipping;

/// <summary>
/// Avaliador puro de elegibilidade J3 (sem DB / sem HTTP / sem XmlCipher).
/// Telefone permanece opcional (alinhado ao mapper Avulso).
/// </summary>
public static class J3FulfillmentEligibility
{
    public const int ChNFeDigitLength = 44;

    public static J3FulfillmentEligibilityResult Evaluate(
        Order? order,
        J3FiscalEligibilitySnapshot? fiscal,
        J3Fulfillment? fulfillment,
        bool fulfillmentEnabled)
    {
        if (!fulfillmentEnabled)
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.FeatureDisabled,
                "J3 fulfillment desabilitado (flag).");
        }

        if (order is null)
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.OrderNotFound,
                "Pedido não encontrado.");
        }

        if (!string.Equals(order.ShippingMethodId, ShippingMethod.J3, StringComparison.OrdinalIgnoreCase))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.WrongShippingMethod,
                "Frete do pedido não é J3.",
                fiscal);
        }

        if (order.Status != OrderStatus.PaymentApproved)
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.PaymentNotApproved,
                "Pedido não está com pagamento aprovado.",
                fiscal);
        }

        if (fiscal is null)
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.MissingFiscalInvoice,
                "FiscalInvoice ausente.");
        }

        if (!string.Equals(fiscal.Status, FiscalInvoiceStatus.Authorized, StringComparison.Ordinal))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized,
                "FiscalInvoice não está authorized.",
                fiscal);
        }

        var chNFe = fiscal.ChNFe?.Trim();
        if (string.IsNullOrEmpty(chNFe))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.MissingNfeKey,
                "ChNFe ausente.",
                fiscal);
        }

        if (!IsValidChNFe(chNFe))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.InvalidNfeKey,
                "ChNFe inválida (exige 44 dígitos).",
                fiscal);
        }

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.MissingCustomerName,
                "Nome do cliente ausente.",
                fiscal);
        }

        if (order.ShippingIsResidentialAddress is null)
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.MissingResidentialFlag,
                "Flag residencial/comercial ausente.",
                fiscal);
        }

        if (BrazilianCep.TryNormalize(order.ShipCep) is null
            || string.IsNullOrWhiteSpace(order.ShipStreet)
            || string.IsNullOrWhiteSpace(order.ShipNumber)
            || string.IsNullOrWhiteSpace(order.ShipNeighborhood)
            || string.IsNullOrWhiteSpace(order.ShipCity)
            || string.IsNullOrWhiteSpace(order.ShipState))
        {
            return J3FulfillmentEligibilityResult.Fail(
                J3FulfillmentEligibilityCodes.IncompleteShippingAddress,
                "Endereço de entrega incompleto.",
                fiscal);
        }

        if (fulfillment is not null)
        {
            switch (fulfillment.Status)
            {
                case J3FulfillmentStatus.Pending:
                    break;
                case J3FulfillmentStatus.Processing:
                    return J3FulfillmentEligibilityResult.Fail(
                        J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists,
                        "Fulfillment já em Processing.",
                        fiscal);
                case J3FulfillmentStatus.Created:
                    return J3FulfillmentEligibilityResult.Fail(
                        J3FulfillmentEligibilityCodes.FulfillmentAlreadyCreated,
                        "Fulfillment já Created na J3.",
                        fiscal);
                case J3FulfillmentStatus.UnknownOutcome:
                    return J3FulfillmentEligibilityResult.Fail(
                        J3FulfillmentEligibilityCodes.UnknownOutcomeRequiresReview,
                        "UnknownOutcome exige revisão manual; sem auto-retry.",
                        fiscal);
                case J3FulfillmentStatus.RetryableFailure:
                    return J3FulfillmentEligibilityResult.Fail(
                        J3FulfillmentEligibilityCodes.RetryableFailureNotAutoRetried,
                        "RetryableFailure sem reprocesso automático neste passo.",
                        fiscal);
                default:
                    return J3FulfillmentEligibilityResult.Fail(
                        J3FulfillmentEligibilityCodes.FulfillmentAlreadyExists,
                        "Status de fulfillment não processável.",
                        fiscal);
            }
        }

        return J3FulfillmentEligibilityResult.Ok(fiscal);
    }

    public static bool IsValidChNFe(string? chNFe)
    {
        if (string.IsNullOrWhiteSpace(chNFe))
            return false;
        var trimmed = chNFe.Trim();
        if (trimmed.Length != ChNFeDigitLength)
            return false;
        foreach (var ch in trimmed)
        {
            if (ch is < '0' or > '9')
                return false;
        }

        return true;
    }

    public static J3FiscalEligibilitySnapshot? SnapshotFiscal(FiscalInvoice? invoice)
    {
        if (invoice is null)
            return null;

        return new J3FiscalEligibilitySnapshot
        {
            Status = invoice.Status,
            ChNFe = invoice.ChNFe,
            Number = invoice.Number,
            Series = invoice.Series,
            AuthorizedAtUtc = invoice.AuthorizedAtUtc
        };
    }

    /// <summary>Mapeia reason → LastErrorCode sanitizável (persistência pós-claim).</summary>
    public static string ToErrorCode(string reasonCode) =>
        reasonCode switch
        {
            J3FulfillmentEligibilityCodes.MissingFiscalInvoice => "MISSING_FISCAL_INVOICE",
            J3FulfillmentEligibilityCodes.FiscalInvoiceNotAuthorized => "FISCAL_NOT_AUTHORIZED",
            J3FulfillmentEligibilityCodes.MissingNfeKey => "MISSING_NFE_KEY",
            J3FulfillmentEligibilityCodes.InvalidNfeKey => "INVALID_NFE_KEY",
            J3FulfillmentEligibilityCodes.IncompleteShippingAddress => J3FulfillmentErrorCodes.MissingAddress,
            J3FulfillmentEligibilityCodes.MissingResidentialFlag => J3FulfillmentErrorCodes.ResidentialRequired,
            J3FulfillmentEligibilityCodes.MissingCustomerName => J3FulfillmentErrorCodes.MissingAddress,
            J3FulfillmentEligibilityCodes.WrongShippingMethod => J3FulfillmentErrorCodes.Configuration,
            J3FulfillmentEligibilityCodes.PaymentNotApproved => J3FulfillmentErrorCodes.Configuration,
            J3FulfillmentEligibilityCodes.FeatureDisabled => J3FulfillmentErrorCodes.FulfillmentDisabled,
            _ => J3FulfillmentErrorCodes.Configuration
        };
}
