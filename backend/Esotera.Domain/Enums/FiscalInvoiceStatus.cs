namespace Esotera.Domain.Enums;

/// <summary>
/// Status fiscal local da NF-e importada. Ausência de FiscalInvoice = aguardando XML.
/// </summary>
public static class FiscalInvoiceStatus
{
    public const string Authorized = "authorized";
    public const string Unknown = "unknown";
}
