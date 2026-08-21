using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Esotera.Application.Common;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Exporta pedido pago preservando o XLSX canônico UpSeller (ZIP/OpenXML).
/// Não usa ClosedXML/EPPlus — apenas ZipArchive + XML cirúrgico.
/// </summary>
public sealed class UpSellerOrderExportService : IUpSellerOrderExportService
{
    public const string SheetName = "order_";
    public const string TemplateResourceName = "Esotera.Infrastructure.Templates.upseller-order-import.xlsx";
    /// <summary>SHA-256 do template canônico comprovadamente aceito pelo UpSeller.</summary>
    public const string ExpectedTemplateSha256 =
        "A8BEBF4411B93A0B83591E6A5A1ECF72BB386CACC56395E98BE68AEF5B4D9F27";
    public const string ExpectedIcvValue = "1596A9933E3543548CF7640D99C60098_13";
    public const int TemplateDataStartRow = 4;
    public const int TemplateMaxItems = 3;

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string WorksheetEntryPath = "xl/worksheets/sheet1.xml";
    private const string SharedStringsEntryPath = "xl/sharedStrings.xml";
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly HashSet<string> EligibleStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        OrderStatus.PaymentApproved,
        OrderStatus.Preparing
    };

    private static readonly HashSet<string> AllowedChangedEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        SharedStringsEntryPath,
        WorksheetEntryPath
    };

    private readonly EsoteraDbContext _context;
    private readonly UpSellerOptions _options;

    public UpSellerOrderExportService(
        EsoteraDbContext context,
        IOptions<UpSellerOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<UpSellerExportFile> ExportOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Pedido", orderId);

        if (!EligibleStatuses.Contains(order.Status))
        {
            throw new ValidationException(
                "status",
                "Exportação UpSeller disponível apenas para pedidos com pagamento aprovado ou em preparo.");
        }

        var items = order.Items.OrderBy(i => i.ProductName).ThenBy(i => i.Id).ToList();
        if (items.Count == 0)
            throw new ValidationException("items", "Pedido sem itens — não é possível exportar para o UpSeller.");

        if (items.Count > TemplateMaxItems)
        {
            throw new ValidationException(
                "items",
                "Exportação UpSeller suporta até 3 itens nesta versão homologada.");
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                throw new ValidationException(
                    "sku",
                    $"Item '{item.ProductName}' sem SKU no pedido (legado ou variação sem SKU). Atualize o cadastro e não invente SKU no export.");
            }
        }

        var stateFull = BrazilianStateNames.TryGetFullName(order.ShipState);
        if (stateFull is null)
        {
            throw new ValidationException(
                "address.state",
                $"Estado/UF do pedido inválido para o UpSeller: '{order.ShipState}'.");
        }

        if (!int.TryParse(_options.ShippingCostMethod?.Trim(), out var shippingCostMethod))
        {
            throw new ValidationException(
                "shippingCostMethod",
                $"Método de custo de envio UpSeller inválido: '{_options.ShippingCostMethod}'.");
        }

        var invoiceRequired = NormalizeInvoiceRequired(_options.InvoiceRequired);
        string? recipientCpfDigits = null;
        if (invoiceRequired == "Sim")
        {
            // Pessoa física: H/I exigem Tipo=CPF + número. Sem modelagem PJ/CNPJ ainda —
            // falhar explicitamente em vez de inventar dados fiscais.
            recipientCpfDigits = TryNormalizeCpf(order.CustomerCpf);
            if (recipientCpfDigits is null)
            {
                throw new ValidationException(
                    "customerCpf",
                    "Necessita Emitir NF-e = Sim exige CPF válido (11 dígitos) no pedido. " +
                    "Exportação com CNPJ/pessoa jurídica ainda não é suportada.");
            }
        }

        var bytes = BuildWorkbook(
            order,
            items,
            stateFull,
            shippingCostMethod,
            invoiceRequired,
            recipientCpfDigits);
        var safeNumber = string.Join("_", order.OrderNumber.Split(Path.GetInvalidFileNameChars()));
        return new UpSellerExportFile(
            bytes,
            $"upseller-pedido-{safeNumber}.xlsx",
            XlsxContentType);
    }

    private byte[] BuildWorkbook(
        Order order,
        IReadOnlyList<OrderItem> items,
        string stateFullName,
        int shippingCostMethod,
        string invoiceRequired,
        string? recipientCpfDigits)
    {
        var templateBytes = ReadEmbeddedTemplateBytes();
        using var input = new MemoryStream(templateBytes);
        using var output = new MemoryStream();

        using (var zipIn = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var zipOut = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var sheetEntry = zipIn.GetEntry(WorksheetEntryPath)
                ?? throw new InvalidOperationException("Template sem sheet1.xml.");
            var ssEntry = zipIn.GetEntry(SharedStringsEntryPath)
                ?? throw new InvalidOperationException("Template sem sharedStrings.xml.");

            string sheetXml;
            string sharedXml;
            using (var sheetStream = sheetEntry.Open())
            using (var reader = new StreamReader(sheetStream, Encoding.UTF8))
                sheetXml = reader.ReadToEnd();
            using (var ssStream = ssEntry.Open())
            using (var reader = new StreamReader(ssStream, Encoding.UTF8))
                sharedXml = reader.ReadToEnd();

            var patcher = new OpenXmlOrderSheetPatcher(sheetXml, sharedXml);
            patcher.EnsureDataRows(items.Count);

            var payment = _options.ResolvePaymentMethod(order.PaymentMethod);
            var cep = FormatCep(order.ShipCep);
            var note = string.IsNullOrWhiteSpace(order.CouponCode)
                ? null
                : $"Cupom {order.CouponCode.Trim()}";
            var nfeSim = invoiceRequired == "Sim";

            for (var i = 0; i < items.Count; i++)
            {
                var row = TemplateDataStartRow + i;
                var item = items[i];
                var isFirst = i == 0;

                patcher.SetSharedString(Col("B", row), _options.StoreName);
                patcher.SetSharedString(Col("C", row), order.OrderNumber);
                if (note is null)
                    patcher.RemoveCell(Col("D", row));
                else
                    patcher.SetSharedString(Col("D", row), note);
                patcher.SetSharedString(Col("E", row), invoiceRequired);
                patcher.SetSharedString(Col("F", row), order.CustomerName);

                if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
                    patcher.SetSharedString(Col("G", row), order.CustomerPhone.Trim());

                if (nfeSim)
                {
                    // Template: H=Tipo de Tributação, I=Número (CPF). PF → sem Empresa/IE.
                    patcher.SetSharedString(Col("H", row), "CPF");
                    patcher.SetSharedString(Col("I", row), recipientCpfDigits!);
                    patcher.RemoveCell(Col("J", row));
                    patcher.RemoveCell(Col("K", row));
                }
                else
                {
                    patcher.RemoveCell(Col("H", row));
                    patcher.RemoveCell(Col("I", row));
                    patcher.RemoveCell(Col("J", row));
                    patcher.RemoveCell(Col("K", row));
                }

                patcher.SetSharedString(Col("L", row), cep);
                patcher.SetSharedString(Col("M", row), stateFullName);
                patcher.SetSharedString(Col("N", row), order.ShipCity);
                patcher.SetSharedString(Col("O", row), order.ShipNeighborhood);
                SetStreetNumber(patcher, Col("P", row), order.ShipNumber);
                patcher.SetSharedString(Col("Q", row), order.ShipStreet);
                if (!string.IsNullOrWhiteSpace(order.ShipComplement))
                    patcher.SetSharedString(Col("R", row), order.ShipComplement.Trim());

                patcher.SetSharedString(Col("S", row), _options.WarehouseName);
                patcher.SetSharedString(Col("T", row), item.Sku!.Trim());
                patcher.SetNumber(Col("U", row), item.Quantity);
                patcher.SetNumber(Col("V", row), item.UnitPrice);
                patcher.SetNumber(Col("W", row), shippingCostMethod);
                patcher.SetNumber(Col("AB", row), _options.PackageQuantity);

                patcher.SetSharedString(Col("AO", row), payment);
                SetOptionalMoney(patcher, Col("AP", row), isFirst ? order.ShippingPrice : 0m);
                SetOptionalMoney(patcher, Col("AQ", row), isFirst ? order.Discount : 0m);
                SetOptionalMoney(patcher, Col("AR", row), 0m);
            }

            var (newSheet, newShared) = patcher.Save();

            foreach (var entry in zipIn.Entries)
            {
                var outInfo = new ZipArchiveEntryMetadata(entry);
                var outEntry = zipOut.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var outStream = outEntry.Open();

                if (string.Equals(entry.FullName, WorksheetEntryPath, StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = Encoding.UTF8.GetBytes(newSheet);
                    outStream.Write(bytes, 0, bytes.Length);
                }
                else if (string.Equals(entry.FullName, SharedStringsEntryPath, StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = Encoding.UTF8.GetBytes(newShared);
                    outStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    using var inStream = entry.Open();
                    inStream.CopyTo(outStream);
                }

                _ = outInfo; // metadata reserved for future fidelity tweaks
            }
        }

        return output.ToArray();
    }

    /// <summary>
    /// Campos monetários opcionais do UpSeller: só grava se &gt; 0.
    /// Zero/ausente → remove a célula (nunca grava 0 como placeholder).
    /// </summary>
    private static void SetOptionalMoney(OpenXmlOrderSheetPatcher patcher, string address, decimal value)
    {
        if (value > 0m)
            patcher.SetNumber(address, value);
        else
            patcher.RemoveCell(address);
    }

    /// <summary>Homologação UpSeller: literais exatamente "Não" / "Sim".</summary>
    private static string NormalizeInvoiceRequired(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (string.Equals(v, "Sim", StringComparison.Ordinal))
            return "Sim";
        // Qualquer outra variação (NÃO, NAO, false, etc.) → Não nesta fase.
        return "Não";
    }

    /// <summary>
    /// CPF com exatamente 11 dígitos (aceita máscara). Null se vazio/inválido.
    /// Sem validação de dígitos verificadores além do comprimento (alinhado ao cadastro).
    /// </summary>
    internal static string? TryNormalizeCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        return digits.Length == 11 ? digits : null;
    }

    private static void SetStreetNumber(OpenXmlOrderSheetPatcher patcher, string address, string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return;

        var trimmed = number.Trim();
        if (decimal.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asNumber))
            patcher.SetNumber(address, asNumber);
        else
            patcher.SetSharedString(address, trimmed);
    }

    private static string Col(string letters, int row) => letters + row.ToString(CultureInfo.InvariantCulture);

    public static byte[] ReadEmbeddedTemplateBytes()
    {
        var assembly = typeof(UpSellerOrderExportService).Assembly;
        using var stream = assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Template UpSeller embutido não encontrado ('{TemplateResourceName}').");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ComputeSha256Hex(byte[] data)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }

    public static IReadOnlyDictionary<string, string> EntrySha256Map(byte[] xlsxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsxBytes), ZipArchiveMode.Read);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            map[entry.FullName] = ComputeSha256Hex(ms.ToArray());
        }

        return map;
    }

    public static IReadOnlyList<string> DiffChangedEntries(byte[] before, byte[] after)
    {
        var a = EntrySha256Map(before);
        var b = EntrySha256Map(after);
        var changed = new List<string>();
        foreach (var (name, hash) in b)
        {
            if (!a.TryGetValue(name, out var prev) || !string.Equals(prev, hash, StringComparison.OrdinalIgnoreCase))
                changed.Add(name);
        }

        foreach (var name in a.Keys)
        {
            if (!b.ContainsKey(name))
                changed.Add(name);
        }

        return changed.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsAllowedChangedEntry(string entryName) =>
        AllowedChangedEntries.Contains(entryName);

    private static string FormatCep(string? cep)
    {
        var digits = BrazilianCep.TryNormalize(cep);
        return digits is null ? (cep ?? string.Empty).Trim() : BrazilianCep.FormatMasked(digits);
    }

    public static string? TryReadIcv(byte[] xlsxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsxBytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry("docProps/custom.xml");
        if (entry is null)
            return null;
        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
        XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
        return doc.Root?
            .Elements(ns + "property")
            .FirstOrDefault(p => (string?)p.Attribute("name") == "ICV")?
            .Element(vt + "lpwstr")?
            .Value;
    }

    private readonly struct ZipArchiveEntryMetadata
    {
        public ZipArchiveEntryMetadata(ZipArchiveEntry entry)
        {
            FullName = entry.FullName;
            LastWriteTime = entry.LastWriteTime;
        }

        public string FullName { get; }
        public DateTimeOffset LastWriteTime { get; }
    }
}

/// <summary>Patch cirúrgico de sheet1 + sharedStrings preservando o restante do pacote.</summary>
internal sealed class OpenXmlOrderSheetPatcher
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly XDocument _sheet;
    private readonly XDocument _shared;
    private readonly List<XElement> _siElements;
    private readonly Dictionary<string, XElement> _cellsByRef;
    private XElement _sheetData;
    private readonly Dictionary<int, HashSet<string>> _siUsers = new();

    public OpenXmlOrderSheetPatcher(string sheetXml, string sharedStringsXml)
    {
        _sheet = XDocument.Parse(sheetXml, LoadOptions.PreserveWhitespace);
        _shared = XDocument.Parse(sharedStringsXml, LoadOptions.PreserveWhitespace);
        _sheetData = _sheet.Root?.Element(Ns + "sheetData")
            ?? throw new InvalidOperationException("sheetData ausente.");
        var sst = _shared.Root ?? throw new InvalidOperationException("sst ausente.");
        _siElements = sst.Elements(Ns + "si").ToList();
        _cellsByRef = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        RebuildCellIndex();
        RebuildSiUsage();
    }

    public void EnsureDataRows(int itemCount)
    {
        if (itemCount <= 1)
            return;

        var templateRow = FindRow(4)
            ?? throw new InvalidOperationException("Template sem linha 4.");

        for (var rowNum = 5; rowNum < 4 + itemCount; rowNum++)
            FindRow(rowNum)?.Remove();

        XElement anchor = templateRow;
        for (var rowNum = 5; rowNum < 4 + itemCount; rowNum++)
        {
            var clone = new XElement(templateRow);
            clone.SetAttributeValue("r", rowNum.ToString(CultureInfo.InvariantCulture));
            foreach (var cell in clone.Elements(Ns + "c"))
            {
                var r = (string?)cell.Attribute("r");
                if (r is null) continue;
                var letters = Regex.Match(r, @"^[A-Z]+", RegexOptions.IgnoreCase).Value.ToUpperInvariant();
                cell.SetAttributeValue("r", letters + rowNum.ToString(CultureInfo.InvariantCulture));
            }

            anchor.AddAfterSelf(clone);
            anchor = clone;
        }

        RebuildCellIndex();
        RebuildSiUsage();
    }

    public void SetSharedString(string cellRef, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        value = value.Trim();
        var cell = GetOrCreateCell(cellRef, preferStyleFrom: null, asSharedString: true);
        var currentIdx = TryGetSharedIndex(cell);
        if (currentIdx is int idx && IsExclusive(idx, cellRef))
        {
            SetSiText(_siElements[idx], value);
            ForceSharedStringCell(cell, idx);
            return;
        }

        var newIdx = AppendSi(value);
        ForceSharedStringCell(cell, newIdx);
        RebuildSiUsage();
    }

    public void RemoveCell(string cellRef)
    {
        if (!_cellsByRef.TryGetValue(cellRef, out var cell))
            return;

        cell.Remove();
        _cellsByRef.Remove(cellRef);
        RebuildSiUsage();
    }

    public void ClearSharedString(string cellRef)
    {
        // Preferência: remover a célula para não deixar <v>índice</v> sem t="s".
        RemoveCell(cellRef);
    }

    public void SetNumber(string cellRef, decimal value)
    {
        var cell = GetOrCreateCell(cellRef, preferStyleFrom: "U4", asSharedString: false);
        cell.Attribute("t")?.Remove();
        if (cell.Attribute("s") is null)
        {
            // Reusa estilo numérico do template quando existir.
            if (_cellsByRef.TryGetValue("U4", out var styleSrc) && styleSrc.Attribute("s") is { } sAttr)
                cell.SetAttributeValue("s", sAttr.Value);
            else
                cell.SetAttributeValue("s", "2");
        }

        var v = cell.Element(Ns + "v");
        var text = FormatNumber(value);
        if (v is null)
            cell.Add(new XElement(Ns + "v", text));
        else
            v.Value = text;

        // Remover is/ inline strings se houver
        cell.Elements(Ns + "is").Remove();
        RebuildSiUsage();
    }

    public (string SheetXml, string SharedStringsXml) Save()
    {
        UpdateSstCounts();
        return (ToXmlString(_sheet), ToXmlString(_shared));
    }

    private XElement GetOrCreateCell(string cellRef, string? preferStyleFrom, bool asSharedString)
    {
        if (_cellsByRef.TryGetValue(cellRef, out var existing))
            return existing;

        var (letters, rowNum) = SplitRef(cellRef);
        var row = FindRow(rowNum)
            ?? throw new InvalidOperationException($"Linha {rowNum} ausente para célula {cellRef}.");

        var cell = new XElement(Ns + "c", new XAttribute("r", cellRef));
        if (asSharedString)
            cell.SetAttributeValue("t", "s");
        else
        {
            if (preferStyleFrom is not null
                && _cellsByRef.TryGetValue(preferStyleFrom, out var styleSrc)
                && styleSrc.Attribute("s") is { } sAttr)
            {
                cell.SetAttributeValue("s", sAttr.Value);
            }
            else
            {
                cell.SetAttributeValue("s", "2");
            }
        }

        cell.Add(new XElement(Ns + "v", asSharedString ? "0" : "0"));

        InsertCellInColumnOrder(row, cell);
        _cellsByRef[cellRef] = cell;
        return cell;
    }

    private static void InsertCellInColumnOrder(XElement row, XElement cell)
    {
        var cellRef = (string)cell.Attribute("r")!;
        var cellCol = ColumnIndex(SplitRef(cellRef).Letters);
        XElement? insertBefore = null;
        foreach (var existing in row.Elements(Ns + "c"))
        {
            var r = (string?)existing.Attribute("r");
            if (r is null) continue;
            if (ColumnIndex(SplitRef(r).Letters) > cellCol)
            {
                insertBefore = existing;
                break;
            }
        }

        if (insertBefore is null)
            row.Add(cell);
        else
            insertBefore.AddBeforeSelf(cell);
    }

    private void ForceSharedStringCell(XElement cell, int index)
    {
        cell.SetAttributeValue("t", "s");
        cell.Attribute("s")?.Remove();
        cell.Elements(Ns + "is").Remove();
        var v = cell.Element(Ns + "v");
        if (v is null)
            cell.Add(new XElement(Ns + "v", index.ToString(CultureInfo.InvariantCulture)));
        else
            v.Value = index.ToString(CultureInfo.InvariantCulture);
    }

    private int AppendSi(string value)
    {
        var sst = _shared.Root!;
        var si = new XElement(Ns + "si", new XElement(Ns + "t", value));
        // Preserve xml:space when leading/trailing whitespace
        if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
            si.Element(Ns + "t")!.SetAttributeValue(XNamespace.Xml + "space", "preserve");

        sst.Add(si);
        _siElements.Add(si);
        return _siElements.Count - 1;
    }

    private static void SetSiText(XElement si, string value)
    {
        // Flatten to a single <t>
        si.Nodes().Remove();
        var t = new XElement(Ns + "t", value);
        if (value.Length == 0 || char.IsWhiteSpace(value[0]) || (value.Length > 0 && char.IsWhiteSpace(value[^1])))
            t.SetAttributeValue(XNamespace.Xml + "space", "preserve");
        si.Add(t);
    }

    private bool IsExclusive(int siIndex, string cellRef)
    {
        if (!_siUsers.TryGetValue(siIndex, out var users))
            return true;
        return users.Count == 1 && users.Contains(cellRef);
    }

    private int? TryGetSharedIndex(XElement cell)
    {
        if ((string?)cell.Attribute("t") != "s")
            return null;
        var v = cell.Element(Ns + "v")?.Value;
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) ? idx : null;
    }

    private void RebuildCellIndex()
    {
        _cellsByRef.Clear();
        foreach (var row in _sheetData.Elements(Ns + "row"))
        {
            foreach (var cell in row.Elements(Ns + "c"))
            {
                var r = (string?)cell.Attribute("r");
                if (r is not null)
                    _cellsByRef[r] = cell;
            }
        }
    }

    private void RebuildSiUsage()
    {
        _siUsers.Clear();
        foreach (var (cellRef, cell) in _cellsByRef)
        {
            var idx = TryGetSharedIndex(cell);
            if (idx is null) continue;
            if (!_siUsers.TryGetValue(idx.Value, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _siUsers[idx.Value] = set;
            }

            set.Add(cellRef);
        }
    }

    private void UpdateSstCounts()
    {
        var sst = _shared.Root!;
        var unique = _siElements.Count;
        var totalRefs = _siUsers.Values.Sum(v => v.Count);
        // count deve refletir referências; uniqueCount = si elements.
        sst.SetAttributeValue("count", Math.Max(totalRefs, unique).ToString(CultureInfo.InvariantCulture));
        sst.SetAttributeValue("uniqueCount", unique.ToString(CultureInfo.InvariantCulture));
    }

    private XElement? FindRow(int rowNum)
    {
        var want = rowNum.ToString(CultureInfo.InvariantCulture);
        return _sheetData.Elements(Ns + "row")
            .FirstOrDefault(r => (string?)r.Attribute("r") == want);
    }

    private static string FormatNumber(decimal value)
    {
        // Invariant: 54.9 not 54,9 — OOXML usa ponto.
        if (value == decimal.Truncate(value) && value is >= int.MinValue and <= int.MaxValue)
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.##############", CultureInfo.InvariantCulture);
    }

    private static (string Letters, int Row) SplitRef(string cellRef)
    {
        var m = Regex.Match(cellRef, @"^(?<c>[A-Z]+)(?<r>\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success)
            throw new ArgumentException($"Referência inválida: {cellRef}");
        return (m.Groups["c"].Value.ToUpperInvariant(), int.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture));
    }

    private static int ColumnIndex(string letters)
    {
        var n = 0;
        foreach (var ch in letters.ToUpperInvariant())
            n = n * 26 + (ch - 'A' + 1);
        return n;
    }

    private static string ToXmlString(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            NewLineHandling = NewLineHandling.None,
            Indent = false
        };

        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
            doc.Save(writer);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
