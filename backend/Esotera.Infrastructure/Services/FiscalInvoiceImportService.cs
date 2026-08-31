using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Esotera.Application.Common;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esotera.Infrastructure.Services;

/// <summary>
/// Importação manual de XML fiscal. Sem J3/UpSeller/SEFAZ HTTP.
/// Matching: documento + (total OU itens) quando os campos estão presentes no parse.
/// </summary>
public sealed class FiscalInvoiceImportService : IFiscalInvoiceImportService
{
    public const long MaxUploadBytes = FiscalInvoiceXmlParser.DefaultMaxXmlBytes;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/xml",
        "text/xml",
        "application/octet-stream" // browsers às vezes enviam assim; conteúdo ainda é validado
    };

    private readonly EsoteraDbContext _db;
    private readonly IFiscalInvoiceXmlParser _parser;
    private readonly IIntegrationsEncryptionService _encryption;
    private readonly IMelhorEnvioShipmentLocalService _melhorEnvioShipment;
    private readonly IMelhorEnvioShipmentProcessingService _cartProcessing;
    private readonly MelhorEnvioOptions _melhorEnvioOptions;
    private readonly ILogger<FiscalInvoiceImportService> _logger;

    public FiscalInvoiceImportService(
        EsoteraDbContext db,
        IFiscalInvoiceXmlParser parser,
        IIntegrationsEncryptionService encryption,
        IMelhorEnvioShipmentLocalService melhorEnvioShipment,
        IMelhorEnvioShipmentProcessingService cartProcessing,
        IOptions<MelhorEnvioOptions> melhorEnvioOptions,
        ILogger<FiscalInvoiceImportService> logger)
    {
        _db = db;
        _parser = parser;
        _encryption = encryption;
        _melhorEnvioShipment = melhorEnvioShipment;
        _cartProcessing = cartProcessing;
        _melhorEnvioOptions = melhorEnvioOptions.Value;
        _logger = logger;
    }

    public async Task<FiscalInvoiceImportResultDto> ImportXmlAsync(
        Guid orderId,
        Stream xmlStream,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadMetadata(fileName, contentType);

        if (!_encryption.IsConfigured)
            throw new ValidationException("encryption", "Criptografia de integrações não configurada.");

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new NotFoundException("Pedido", orderId);

        var bytes = await ReadLimitedAsync(xmlStream, cancellationToken);
        ValidateLooksLikeXml(bytes);

        var shaHex = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var parsed = _parser.Parse(bytes);

        // Idempotência: mesmo Order + mesma chave ou mesmo hash
        var existingSame = await _db.FiscalInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.OrderId == orderId
                     && (f.ChNFe == parsed.ChNFe || f.XmlSha256 == shaHex),
                cancellationToken);

        if (existingSame is not null)
        {
            _logger.LogInformation(
                "FiscalInvoice import idempotent replay for OrderId={OrderId} Status={Status}",
                orderId,
                existingSame.Status);
            return ToResult(existingSame, idempotent: true);
        }

        var otherOrderSameKey = await _db.FiscalInvoices
            .AsNoTracking()
            .AnyAsync(f => f.ChNFe == parsed.ChNFe && f.OrderId != orderId, cancellationToken);
        if (otherOrderSameKey)
            throw new ConflictException("Esta chNFe já está vinculada a outro pedido.");

        var hasAuthorized = await _db.FiscalInvoices
            .AsNoTracking()
            .AnyAsync(
                f => f.OrderId == orderId && f.Status == FiscalInvoiceStatus.Authorized,
                cancellationToken);
        if (hasAuthorized)
            throw new ConflictException("Este pedido já possui uma NF-e autorizada. Não sobrescrevemos silenciosamente.");

        ValidateOrderMatch(order, parsed);

        var status = parsed.HasAuthorizationEvidence
            ? FiscalInvoiceStatus.Authorized
            : FiscalInvoiceStatus.Unknown;

        // Não logar XML nem documentos.
        var xmlText = Encoding.UTF8.GetString(bytes);
        var cipher = _encryption.Encrypt(xmlText);

        var now = DateTime.UtcNow;
        var entity = new FiscalInvoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = status,
            ChNFe = parsed.ChNFe,
            Number = parsed.Number,
            Series = parsed.Series,
            Environment = parsed.Environment,
            IssuedAtUtc = parsed.IssuedAtUtc,
            AuthorizedAtUtc = parsed.AuthorizedAtUtc,
            IssuerCnpj = parsed.IssuerCnpj,
            RecipientDocument = parsed.RecipientDocument,
            ProtocolNumber = parsed.ProtocolNumber,
            XmlCipher = cipher,
            XmlSha256 = shaHex,
            Source = FiscalInvoiceSource.ManualUpload,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.FiscalInvoices.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Conflito ao gravar NF-e (possível duplicidade de chNFe ou autorizada).");
        }

        _logger.LogInformation(
            "FiscalInvoice imported OrderId={OrderId} Status={Status} ShaPrefix={ShaPrefix}",
            orderId,
            status,
            shaHex[..Math.Min(8, shaHex.Length)]);

        // NF-e autorizada libera o envio Melhor Envio para criação: promove o registro
        // local waiting_invoice → ready_to_create.
        if (status == FiscalInvoiceStatus.Authorized)
        {
            await _melhorEnvioShipment.SyncInvoiceReadinessAsync(orderId, cancellationToken);
            await TryAutoCreateCartShipmentAsync(orderId, cancellationToken);
        }

        return ToResult(entity, idempotent: false);
    }

    /// <summary>
    /// Insere o frete no carrinho do Melhor Envio logo após a NF-e ser autorizada,
    /// SOMENTE se MELHOR_ENVIO_AUTO_CREATE_CART_SHIPMENT=true (default false).
    /// Nunca compra etiqueta. Falha aqui não derruba a importação da NF-e: o erro
    /// fica registrado no shipment e o Admin oferece o botão manual.
    /// </summary>
    private async Task TryAutoCreateCartShipmentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!_melhorEnvioOptions.AutoCreateCartShipment)
            return;

        try
        {
            var result = await _cartProcessing.CreateCartShipmentAsync(orderId, cancellationToken);
            if (!result.Ok)
            {
                _logger.LogInformation(
                    "Melhor Envio auto-cart: pedido {OrderId} não criado (code={Code})",
                    orderId,
                    result.ErrorCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Melhor Envio auto-cart: falha inesperada no pedido {OrderId}",
                orderId);
        }
    }

    /// <summary>Tolerância monetária explícita (documento + total fallback).</summary>
    public const decimal TotalMatchTolerance = 0.05m;

    /// <summary>
    /// Matching v1: documento obrigatório + (itens preferencial quando há SKU, senão total).
    /// Não usa xPed / nome / endereço. SKU comparado via <see cref="FiscalSkuNormalizer"/>.
    /// </summary>
    public static void ValidateOrderMatch(Order order, FiscalInvoiceParseResult parsed)
    {
        if (string.IsNullOrWhiteSpace(parsed.RecipientDocument))
            throw new ValidationException("recipient", "XML sem documento do destinatário — não é possível associar ao pedido.");

        var orderDoc = DigitsOnly(order.CustomerCpf);
        if (string.IsNullOrWhiteSpace(orderDoc))
            throw new ValidationException("recipient", "Pedido sem CPF do cliente — não é possível validar o XML.");

        if (!string.Equals(orderDoc, parsed.RecipientDocument, StringComparison.Ordinal))
            throw new ValidationException("recipient", "Documento do destinatário no XML não confere com o pedido.");

        var orderHasSku = order.Items.Any(i => !string.IsNullOrWhiteSpace(i.Sku));
        var xmlHasItems = parsed.Items.Count > 0;
        var itemsOk = ItemsMatch(order, parsed);
        var totalOk = parsed.InvoiceTotal is { } total
            && Math.Abs(total - order.Total) <= TotalMatchTolerance;

        // Preferência: documento + itens quando ambos têm SKU.
        if (orderHasSku && xmlHasItems)
        {
            if (!itemsOk)
            {
                throw new ValidationException(
                    "match",
                    "Itens do XML não conferem com o pedido (SKU/quantidade). Associação recusada.");
            }

            return;
        }

        // Fallback: documento + total (ou itens se ainda casarem).
        if (totalOk || itemsOk)
            return;

        throw new ValidationException(
            "match",
            "XML não confere com o pedido (total e itens). Associação recusada.");
    }

    private static bool ItemsMatch(Order order, FiscalInvoiceParseResult parsed)
    {
        if (parsed.Items.Count == 0)
            return false;

        var orderSkus = order.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Sku))
            .GroupBy(i => FiscalSkuNormalizer.Normalize(i.Sku), StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => (decimal)g.Sum(x => x.Quantity), StringComparer.Ordinal);

        if (orderSkus.Count == 0)
            return false;

        return parsed.Items.All(p =>
        {
            var key = FiscalSkuNormalizer.Normalize(p.Sku);
            return key.Length > 0
                   && orderSkus.TryGetValue(key, out var qty)
                   && qty == p.Quantity;
        });
    }

    private static void ValidateUploadMetadata(string? fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var ext = Path.GetExtension(fileName);
            if (!string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("file", "Somente arquivos .xml são aceitos.");
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && !AllowedContentTypes.Contains(contentType.Split(';')[0].Trim()))
        {
            throw new ValidationException("file", "Content-Type inválido para XML.");
        }
    }

    private static void ValidateLooksLikeXml(byte[] bytes)
    {
        // Não confiar só em extensão/MIME: exige '<' inicial (após BOM/whitespace).
        var span = bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];
        var i = 0;
        while (i < span.Length && (span[i] == (byte)' ' || span[i] == (byte)'\t' || span[i] == (byte)'\r' || span[i] == (byte)'\n'))
            i++;
        if (i >= span.Length || span[i] != (byte)'<')
            throw new ValidationException("file", "Conteúdo não parece XML.");
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream stream, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            total += read;
            if (total > MaxUploadBytes)
                throw new ValidationException("file", "Arquivo XML excede o limite de 2 MB.");
            await ms.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        if (ms.Length == 0)
            throw new ValidationException("file", "Arquivo XML vazio.");

        return ms.ToArray();
    }

    private static FiscalInvoiceImportResultDto ToResult(FiscalInvoice entity, bool idempotent) =>
        new(
            entity.Status,
            MaskChNFe(entity.ChNFe),
            entity.Number,
            entity.Series,
            entity.AuthorizedAtUtc,
            idempotent);

    public static string MaskChNFe(string? chNFe)
    {
        if (string.IsNullOrWhiteSpace(chNFe) || chNFe.Length < 6)
            return "••••";
        return new string('•', chNFe.Length - 6) + chNFe[^6..];
    }

    private static string DigitsOnly(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}
