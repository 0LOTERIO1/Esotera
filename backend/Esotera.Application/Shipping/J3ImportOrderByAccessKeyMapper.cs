using Esotera.Application.Common;
using Esotera.Application.DTOs.Fiscal;
using Esotera.Application.DTOs.J3;
using Esotera.Application.Options;
using Esotera.Domain.Entities;

namespace Esotera.Application.Shipping;

/// <summary>
/// Mapper puro: FiscalInvoiceParseResult (+ telefone Order opcional só para dest) → ImportOrderByAccessKeyInput.
/// emitEnder.fone: XML enderEmit/fone → J3_EMITTER_PHONE (dígitos) → fail-closed.
/// Sem HTTP. Sem XmlCipher. Sem inventar emitXFant: usa xFant do XML ou, se ausente, xNome do emit.
/// </summary>
public static class J3ImportOrderByAccessKeyMapper
{
    public static J3ImportOrderByAccessKeyBuildResult TryBuild(
        Order order,
        FiscalInvoiceParseResult parsed,
        J3ShippingOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(options);

        if (!parsed.HasAuthorizationEvidence)
            return Fail(J3FulfillmentErrorCodes.Configuration);

        if (!J3FulfillmentEligibility.IsValidChNFe(parsed.ChNFe))
            return Fail("INVALID_NFE_KEY");

        var sellerInformationId = options.SellerInformationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sellerInformationId))
            return Fail(J3FulfillmentErrorCodes.MissingSellerInformationId);

        var sellerId = string.IsNullOrWhiteSpace(options.SellerId) ? null : options.SellerId.Trim();

        var emitXNome = parsed.IssuerName?.Trim();
        if (string.IsNullOrWhiteSpace(emitXNome))
            return Fail("MISSING_EMIT_XNOME");

        // xFant ausente no XML: usar xNome do emit (dado da NF, não inventado).
        var emitXFant = string.IsNullOrWhiteSpace(parsed.IssuerTradeName)
            ? emitXNome
            : parsed.IssuerTradeName.Trim();

        var destXNome = parsed.RecipientName?.Trim();
        if (string.IsNullOrWhiteSpace(destXNome))
            destXNome = order.CustomerName?.Trim();
        if (string.IsNullOrWhiteSpace(destXNome))
            return Fail("MISSING_DEST_XNOME");

        // Emit: XML → J3_EMITTER_PHONE. Nunca CustomerPhone / seller / Users.
        var emitFallbackPhone = DigitsOrNull(options.EmitterPhone);
        var emitEnder = TryMapAddress(parsed.IssuerAddress, fallbackPhone: emitFallbackPhone);
        if (emitEnder is null)
            return Fail("MISSING_EMIT_ENDER");

        var destFallbackPhone = DigitsOrNull(order.CustomerPhone);
        var destEnder = TryMapAddress(parsed.RecipientAddress, fallbackPhone: destFallbackPhone);
        if (destEnder is null)
            return Fail("MISSING_DEST_ENDER");

        if (parsed.Items.Count == 0)
            return Fail("MISSING_NFE_ITEMS");

        var items = new List<J3NfeDetInputDto>(parsed.Items.Count);
        foreach (var item in parsed.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
                return Fail("MISSING_ITEM_XPROD");
            if (item.UnitPrice is null)
                return Fail("MISSING_ITEM_VUNCOM");
            if (!TryToIntQuantity(item.Quantity, out var qCom))
                return Fail("INVALID_ITEM_QCOM");

            items.Add(new J3NfeDetInputDto
            {
                QCom = qCom,
                VUnCom = (double)item.UnitPrice.Value,
                XProd = item.ProductName.Trim()
            });
        }

        var input = new J3ImportOrderByAccessKeyInputDto
        {
            SellerId = sellerId,
            SellerInformationId = sellerInformationId,
            Order = new J3NfeDataInputDto
            {
                ChNFe = parsed.ChNFe.Trim(),
                DestXNome = destXNome,
                EmitXNome = emitXNome,
                EmitXFant = emitXFant,
                DestEnder = destEnder,
                EmitEnder = emitEnder,
                Items = items
            }
        };

        return new J3ImportOrderByAccessKeyBuildResult
        {
            IsValid = true,
            Command = new J3ImportOrderByAccessKeyCommand
            {
                LocalOrderId = order.Id,
                Input = input
            }
        };
    }

    private static J3NfeAddressInputDto? TryMapAddress(
        FiscalNfeAddressSnapshot? addr,
        string? fallbackPhone)
    {
        if (addr is null)
            return null;

        var zipDigits = addr.ZipCodeDigits;
        if (zipDigits is null || BrazilianCep.TryNormalize(zipDigits) is not { } normalized)
            return null;

        var phone = DigitsOrNull(addr.PhoneDigits) ?? DigitsOrNull(fallbackPhone);
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        if (string.IsNullOrWhiteSpace(addr.Street) || string.IsNullOrWhiteSpace(addr.Number))
            return null;

        return new J3NfeAddressInputDto
        {
            Cep = BrazilianCep.FormatMasked(normalized),
            Fone = phone,
            Nro = addr.Number.Trim(),
            XLgr = addr.Street.Trim(),
            XCpl = string.IsNullOrWhiteSpace(addr.Complement) ? null : addr.Complement.Trim()
        };
    }

    /// <summary>Preserva somente dígitos (convenção do payload J3 / mapper).</summary>
    public static string? DigitsOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    /// <summary>qCom GraphQL Int!: aceita apenas quantidades inteiras ≥ 1.</summary>
    internal static bool TryToIntQuantity(decimal qty, out int value)
    {
        value = 0;
        if (qty < 1m)
            return false;
        if (qty != decimal.Truncate(qty))
            return false;
        if (qty > int.MaxValue)
            return false;
        value = (int)qty;
        return true;
    }

    private static J3ImportOrderByAccessKeyBuildResult Fail(string code) =>
        new()
        {
            IsValid = false,
            ErrorCode = J3FulfillmentErrorCodes.Sanitize(code) ?? J3FulfillmentErrorCodes.Configuration
        };
}

public sealed class J3ImportOrderByAccessKeyBuildResult
{
    public bool IsValid { get; init; }
    public J3ImportOrderByAccessKeyCommand? Command { get; init; }
    public string? ErrorCode { get; init; }
}
