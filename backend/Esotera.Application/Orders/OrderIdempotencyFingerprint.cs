using System.Security.Cryptography;
using System.Text;
using Esotera.Application.DTOs.Orders;

namespace Esotera.Application.Orders;

/// <summary>
/// Impressão estável da compra para detectar reuso de Idempotency-Key com conteúdo diferente.
/// Calculada apenas no servidor.
/// </summary>
public static class OrderIdempotencyFingerprint
{
    public static string Compute(CreateOrderRequest request)
    {
        var items = request.Items
            .OrderBy(i => i.ProductId)
            .ThenBy(i => i.Variation ?? string.Empty, StringComparer.Ordinal)
            .Select(i =>
                $"{i.ProductId:N}:{i.Quantity}:{(i.Variation ?? string.Empty).Trim().ToLowerInvariant()}");

        var addressPart = request.AddressId.HasValue
            ? request.AddressId.Value.ToString("N")
            : request.Address != null
                ? NormalizeAddress(request.Address)
                : string.Empty;

        var coupon = (request.CouponCode ?? string.Empty).Trim().ToUpperInvariant();
        var installments = request.Installments?.ToString() ?? string.Empty;

        var raw = string.Join(";", items)
            + $"|{addressPart}|{request.ShippingMethodId}|{request.PaymentMethod}|{installments}|{coupon}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeAddress(OrderAddressInput address)
    {
        var cep = new string(address.Cep.Where(char.IsDigit).ToArray());
        return string.Join(":",
            cep,
            address.Street.Trim().ToLowerInvariant(),
            address.Number.Trim().ToLowerInvariant(),
            (address.Complement ?? string.Empty).Trim().ToLowerInvariant(),
            address.Neighborhood.Trim().ToLowerInvariant(),
            address.City.Trim().ToLowerInvariant(),
            address.State.Trim().ToUpperInvariant());
    }
}
