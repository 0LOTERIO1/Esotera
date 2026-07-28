using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Esotera.Infrastructure.Persistence;

/// <summary>
/// Garante taxonomia mínima do catálogo em qualquer ambiente (incl. produção).
/// Idempotente: não duplica categorias/produto de referência existentes.
/// Não cria produtos de demonstração (IsDemo) — isso fica no DevSeed.
/// </summary>
public class CatalogBootstrap
{
    private static readonly Guid CategoryTarosId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid CategoryLivrosId = Guid.Parse("00000000-0000-0000-0001-000000000002");
    private static readonly Guid CategoryAcessoriosId = Guid.Parse("00000000-0000-0000-0001-000000000003");
    private static readonly Guid ProductWaiteInicianteId = Guid.Parse("11111111-1111-1111-1111-111111111107");

    private readonly EsoteraDbContext _context;
    private readonly ILogger<CatalogBootstrap> _logger;

    public CatalogBootstrap(EsoteraDbContext context, ILogger<CatalogBootstrap> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCategoriesAsync(cancellationToken);
        await EnsureWaiteInicianteAsync(cancellationToken);
    }

    private async Task EnsureCategoriesAsync(CancellationToken cancellationToken)
    {
        var required = new[]
        {
            new Category { Id = CategoryTarosId, Name = "Tarôs", Slug = "taros" },
            new Category { Id = CategoryLivrosId, Name = "Livros", Slug = "livros" },
            new Category { Id = CategoryAcessoriosId, Name = "Acessórios", Slug = "acessorios" }
        };

        var existingSlugs = await _context.Categories
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);

        var missing = required.Where(c => !existingSlugs.Contains(c.Slug)).ToList();
        if (missing.Count == 0)
            return;

        _context.Categories.AddRange(missing);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Catálogo: {Count} categoria(s) básica(s) criada(s) ({Slugs}).",
            missing.Count,
            string.Join(", ", missing.Select(c => c.Slug)));
    }

    private async Task EnsureWaiteInicianteAsync(CancellationToken cancellationToken)
    {
        const string slug = "rider-waite-taro-esotera-para-iniciante";
        const string variationsJson =
            """[{"id":"var-somente-taro","name":"Somente Tarô","price":54.90,"isAvailable":true,"sku":"SKU-WAITE-TAROT"},{"id":"var-taro-livro","name":"Tarô + Livro","price":79.90,"isAvailable":true,"sku":"SKU-WAITE-KIT"},{"id":"var-somente-livro","name":"Somente Livro","price":0,"isAvailable":false,"sku":"SKU-WAITE-LIVRO"}]""";

        var categoryId = await _context.Categories
            .Where(c => c.Slug == "taros")
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (categoryId == Guid.Empty)
            categoryId = CategoryTarosId;

        var exists = await _context.Products
            .AnyAsync(p => p.Slug == slug || p.Id == ProductWaiteInicianteId, cancellationToken);
        if (exists)
            return;

        var now = DateTime.UtcNow;
        _context.Products.Add(new Product
        {
            Id = ProductWaiteInicianteId,
            Slug = slug,
            Name = "Rider Waite Tarô Esotera para Iniciante com 78 Cartas, Ilustrações e Explicações nas Cartas",
            ShortDescription = "Edição para iniciantes com ilustrações e explicações nas cartas.",
            Description = "Rider Waite Tarô Esotera para iniciantes com 78 cartas ilustradas e explicativas. Escolha entre somente o tarô ou o kit com livro.",
            Price = 54.90m,
            CategoryId = categoryId,
            FeaturesJson = "[\"78 cartas\",\"Ilustrações e explicações nas cartas\",\"Ideal para iniciantes\"]",
            PackageContentsJson = "[\"78 cartas ilustradas\"]",
            VariationsJson = variationsJson,
            IsFeatured = true,
            IsAvailable = true,
            IsDemo = false,
            IsArchived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Images =
            [
                new ProductImage
                {
                    Id = Guid.NewGuid(),
                    SecureUrl = "/images/products/waite-iniciante.png",
                    SortOrder = 1,
                    IsPrimary = true,
                    CreatedAtUtc = now
                }
            ]
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Catálogo: produto de referência Waite Iniciante criado.");
    }
}
