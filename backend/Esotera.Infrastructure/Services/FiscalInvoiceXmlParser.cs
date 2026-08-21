using System.Globalization;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Parser NF-e seguro (DTD/XXE off). Preferência por caminhos estruturais nfeProc + namespace portal fiscal.
/// Sem validação de assinatura X509. Nunca loga XML/PII.
/// </summary>
public sealed class FiscalInvoiceXmlParser : IFiscalInvoiceXmlParser
{
    public const int DefaultMaxXmlBytes = 2 * 1024 * 1024;
    public static readonly XNamespace NfeNs = "http://www.portalfiscal.inf.br/nfe";

    public int MaxXmlBytes => DefaultMaxXmlBytes;

    public FiscalInvoiceParseResult Parse(ReadOnlyMemory<byte> xmlUtf8)
    {
        if (xmlUtf8.Length == 0)
            throw new ValidationException("file", "Arquivo XML vazio.");

        if (xmlUtf8.Length > MaxXmlBytes)
            throw new ValidationException("file", "Arquivo XML excede o limite de 2 MB.");

        XDocument doc;
        try
        {
            doc = LoadSecure(xmlUtf8);
        }
        catch (XmlException)
        {
            throw new ValidationException("file", "XML malformado ou inseguro.");
        }
        catch (SecurityException)
        {
            throw new ValidationException("file", "XML rejeitado por política de segurança (DTD/entidades).");
        }

        var root = doc.Root
            ?? throw new ValidationException("file", "XML sem elemento raiz.");

        // Preferência: nfeProc oficial; tolerante a wrapper sem namespace default.
        var nfeProc = Local(root, "nfeProc") ?? (LocalName(root) == "nfeProc" ? root : null);
        var nfe = nfeProc is not null
            ? Child(nfeProc, "NFe")
            : Local(root, "NFe") ?? (LocalName(root) == "NFe" ? root : null);

        var infNFe = nfe is null ? null : Child(nfe, "infNFe");
        if (infNFe is null)
            throw new ValidationException("file", "XML sem infNFe — estrutura NF-e não reconhecida.");

        var ide = Child(infNFe, "ide");
        var emit = Child(infNFe, "emit");
        var dest = Child(infNFe, "dest");
        var total = Child(Child(infNFe, "total"), "ICMSTot");

        var protNFe = nfeProc is not null
            ? Child(nfeProc, "protNFe")
            : FirstDescendantLocal(root, "protNFe");
        var infProt = protNFe is null ? null : Child(protNFe, "infProt");

        var idAttr = (string?)infNFe.Attribute("Id");
        if (string.IsNullOrWhiteSpace(idAttr) || !idAttr.StartsWith("NFe", StringComparison.Ordinal))
            throw new ValidationException("chNFe", "infNFe/@Id inválido (esperado prefixo NFe).");

        var chFromId = DigitsOnly(idAttr["NFe".Length..]);
        if (chFromId.Length != 44)
            throw new ValidationException("chNFe", "chNFe em infNFe/@Id inválida (esperado 44 dígitos).");

        string chNFe;
        string? protocol = null;
        string? cStat = null;
        string? xMotivo = null;
        string? dhRecbtoRaw = null;

        if (infProt is not null)
        {
            var chProt = DigitsOnly(Text(infProt, "chNFe") ?? string.Empty);
            if (chProt.Length != 44 || !chProt.All(char.IsDigit))
                throw new ValidationException("chNFe", "chNFe do protocolo inválida (esperado 44 dígitos).");

            if (!string.Equals(chFromId, chProt, StringComparison.Ordinal))
            {
                throw new ValidationException(
                    "chNFe",
                    "chNFe do infNFe/@Id diverge da chNFe do protocolo — importação rejeitada.");
            }

            chNFe = chProt;
            protocol = NullIfWhite(Text(infProt, "nProt"));
            cStat = NullIfWhite(Text(infProt, "cStat"));
            xMotivo = NullIfWhite(Text(infProt, "xMotivo"));
            dhRecbtoRaw = Text(infProt, "dhRecbto");
        }
        else
        {
            chNFe = chFromId;
        }

        var authorized = IsAuthorizedProtocol(chNFe, cStat, xMotivo, protocol, dhRecbtoRaw);

        var recipientDoc = DigitsOrNull(Text(dest, "CPF") ?? Text(dest, "CNPJ"));

        return new FiscalInvoiceParseResult
        {
            ChNFe = chNFe,
            Number = NullIfWhite(Text(ide, "nNF")),
            Series = NullIfWhite(Text(ide, "serie")),
            Model = NullIfWhite(Text(ide, "mod")),
            Environment = NullIfWhite(Text(ide, "tpAmb")),
            IssuedAtUtc = TryParseXmlDateUtc(Text(ide, "dhEmi")),
            AuthorizedAtUtc = authorized ? TryParseXmlDateUtc(dhRecbtoRaw) : null,
            IssuerCnpj = DigitsOrNull(Text(emit, "CNPJ")),
            IssuerCrt = NullIfWhite(Text(emit, "CRT")),
            IssuerName = NullIfWhite(Text(emit, "xNome")),
            IssuerTradeName = NullIfWhite(Text(emit, "xFant")),
            IssuerAddress = ParseAddress(Child(emit, "enderEmit")),
            RecipientDocument = recipientDoc,
            RecipientName = NullIfWhite(Text(dest, "xNome")),
            RecipientAddress = ParseAddress(Child(dest, "enderDest")),
            ProtocolNumber = protocol,
            ProtocolStatusCode = cStat,
            ProtocolStatusMessage = xMotivo,
            InvoiceTotal = TryParseDecimal(Text(total, "vNF")),
            Items = ParseItems(infNFe),
            HasAuthorizationEvidence = authorized
        };
    }

    private static FiscalNfeAddressSnapshot? ParseAddress(XElement? ender)
    {
        if (ender is null)
            return null;

        var street = NullIfWhite(Text(ender, "xLgr"));
        var number = NullIfWhite(Text(ender, "nro"));
        var complement = NullIfWhite(Text(ender, "xCpl"));
        var zip = DigitsOrNull(Text(ender, "CEP"));
        var phone = DigitsOrNull(Text(ender, "fone"));

        if (street is null && number is null && zip is null && phone is null && complement is null)
            return null;

        return new FiscalNfeAddressSnapshot
        {
            Street = street,
            Number = number,
            Complement = complement,
            ZipCodeDigits = zip,
            PhoneDigits = phone
        };
    }

    /// <summary>
    /// Autorizada somente com evidência completa no protNFe/infProt.
    /// </summary>
    internal static bool IsAuthorizedProtocol(
        string? chNFe,
        string? cStat,
        string? xMotivo,
        string? nProt,
        string? dhRecbto)
    {
        if (string.IsNullOrWhiteSpace(chNFe) || DigitsOnly(chNFe).Length != 44)
            return false;
        if (!string.Equals(cStat?.Trim(), "100", StringComparison.Ordinal))
            return false;
        if (string.IsNullOrWhiteSpace(nProt))
            return false;
        if (string.IsNullOrWhiteSpace(dhRecbto))
            return false;
        if (!IsAuthorizationMotivo(xMotivo))
            return false;
        return true;
    }

    private static bool IsAuthorizationMotivo(string? xMotivo)
    {
        if (string.IsNullOrWhiteSpace(xMotivo))
            return false;
        // SEFAZ típico: "Autorizado o uso da NF-e"
        var m = xMotivo.Trim();
        return m.Contains("autoriz", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FiscalInvoiceParsedItem> ParseItems(XElement infNFe)
    {
        var list = new List<FiscalInvoiceParsedItem>();
        foreach (var det in Children(infNFe, "det"))
        {
            var prod = Child(det, "prod");
            if (prod is null) continue;

            var sku = Text(prod, "cProd");
            var qtyRaw = Text(prod, "qCom");
            if (string.IsNullOrWhiteSpace(sku))
                continue;
            if (!decimal.TryParse(qtyRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty))
                continue;

            list.Add(new FiscalInvoiceParsedItem(
                Sku: sku.Trim(),
                Quantity: qty,
                UnitPrice: TryParseDecimal(Text(prod, "vUnCom")),
                LineTotal: TryParseDecimal(Text(prod, "vProd")),
                Ncm: NullIfWhite(Text(prod, "NCM")),
                Cfop: NullIfWhite(Text(prod, "CFOP")),
                Unit: NullIfWhite(Text(prod, "uCom")),
                ExternalOrderRef: NullIfWhite(Text(prod, "xPed")),
                ProductName: NullIfWhite(Text(prod, "xProd"))));
        }

        return list;
    }

    private static XDocument LoadSecure(ReadOnlyMemory<byte> xmlUtf8)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = DefaultMaxXmlBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false
        };

        using var stream = new MemoryStream(xmlUtf8.ToArray(), writable: false);
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string LocalName(XElement el) => el.Name.LocalName;

    private static XElement? Local(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static XElement? Child(XElement? parent, string localName) =>
        parent is null ? null : Local(parent, localName);

    private static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);

    private static XElement? FirstDescendantLocal(XElement root, string localName) =>
        root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? Text(XElement? parent, string localName) =>
        Child(parent, localName)?.Value?.Trim();

    private static string DigitsOnly(string value) =>
        new string(value.Where(char.IsDigit).ToArray());

    private static string? DigitsOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var d = DigitsOnly(value);
        return d.Length == 0 ? null : d;
    }

    private static string? NullIfWhite(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? TryParseXmlDateUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return dto.UtcDateTime;
        return null;
    }

    private static decimal? TryParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }
}
