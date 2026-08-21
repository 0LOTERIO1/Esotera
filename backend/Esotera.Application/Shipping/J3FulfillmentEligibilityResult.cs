namespace Esotera.Application.Shipping;

/// <summary>
/// Resultado estruturado do gate local J3. Snapshot fiscal sem XmlCipher.
/// </summary>
public sealed class J3FulfillmentEligibilityResult
{
    public required bool IsEligible { get; init; }
    public required string ReasonCode { get; init; }
    public required string Message { get; init; }

    /// <summary>
    /// Recorte fiscal para J3-2 (ChNFe/Number/Series). Nunca inclui XML/cipher.
    /// Presente quando há FiscalInvoice associada, mesmo se não elegível.
    /// </summary>
    public J3FiscalEligibilitySnapshot? Fiscal { get; init; }

    public static J3FulfillmentEligibilityResult Ok(J3FiscalEligibilitySnapshot? fiscal = null) =>
        new()
        {
            IsEligible = true,
            ReasonCode = J3FulfillmentEligibilityCodes.Eligible,
            Message = "Pedido elegível para processamento J3.",
            Fiscal = fiscal
        };

    public static J3FulfillmentEligibilityResult Fail(
        string reasonCode,
        string message,
        J3FiscalEligibilitySnapshot? fiscal = null) =>
        new()
        {
            IsEligible = false,
            ReasonCode = reasonCode,
            Message = message,
            Fiscal = fiscal
        };
}

/// <summary>Campos fiscais necessários ao payload futuro — sem XML.</summary>
public sealed record J3FiscalEligibilitySnapshot
{
    public required string Status { get; init; }
    public string? ChNFe { get; init; }
    public string? Number { get; init; }
    public string? Series { get; init; }
    public DateTime? AuthorizedAtUtc { get; init; }
}
