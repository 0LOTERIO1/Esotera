using System.Text.Json;
using Esotera.Application.DTOs.Products;

namespace Esotera.Infrastructure.Services;

public static class ProductVariationJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Aceita formato novo (id/name/price) e legado ({ type, options[] }).
    /// </summary>
    public static ProductVariationDto[] Parse(string? json, decimal fallbackPrice)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ProductVariationDto>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<ProductVariationDto>();

            var list = new List<ProductVariationDto>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("name", out _) || el.TryGetProperty("Name", out _))
                {
                    var dto = JsonSerializer.Deserialize<ProductVariationDto>(el.GetRawText(), JsonOptions);
                    if (dto != null && !string.IsNullOrWhiteSpace(dto.Name))
                    {
                        list.Add(dto with
                        {
                            Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
                            Price = dto.Price > 0 ? dto.Price : (dto.IsAvailable ? fallbackPrice : 0)
                        });
                    }
                    continue;
                }

                // Legado: { type, options: ["A","B"] }
                if (el.TryGetProperty("options", out var options) || el.TryGetProperty("Options", out options))
                {
                    if (options.ValueKind != JsonValueKind.Array) continue;
                    foreach (var opt in options.EnumerateArray())
                    {
                        var name = opt.GetString();
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        list.Add(new ProductVariationDto(
                            Guid.NewGuid().ToString("N"),
                            name!,
                            fallbackPrice,
                            true,
                            null,
                            null));
                    }
                }
            }

            return list.ToArray();
        }
        catch
        {
            return Array.Empty<ProductVariationDto>();
        }
    }

    public static string Serialize(ProductVariationDto[]? variations) =>
        JsonSerializer.Serialize(variations ?? Array.Empty<ProductVariationDto>(), JsonOptions);

    public static ProductVariationDto? Resolve(
        ProductVariationDto[] variations,
        string? variationIdOrName)
    {
        if (variations.Length == 0)
            return null;

        if (string.IsNullOrWhiteSpace(variationIdOrName))
            return null;

        var key = variationIdOrName.Trim();
        return variations.FirstOrDefault(v =>
                   string.Equals(v.Id, key, StringComparison.OrdinalIgnoreCase))
               ?? variations.FirstOrDefault(v =>
                   string.Equals(v.Name, key, StringComparison.OrdinalIgnoreCase));
    }
}
