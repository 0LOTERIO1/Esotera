using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Esotera.Infrastructure.Persistence;

public class DevSeed
{
    private readonly EsoteraDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DevSeed> _logger;

    private static readonly Guid CategoryTarosId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid CategoryLivrosId = Guid.Parse("00000000-0000-0000-0001-000000000002");
    private static readonly Guid CategoryAcessoriosId = Guid.Parse("00000000-0000-0000-0001-000000000003");

    private static readonly Guid ProductWaiteTradId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid ProductWaitePocketId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid ProductCrowleyId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    private static readonly Guid ProductMarselhaId = Guid.Parse("11111111-1111-1111-1111-111111111104");
    private static readonly Guid ProductLivro78Id = Guid.Parse("11111111-1111-1111-1111-111111111105");
    private static readonly Guid ProductToadaId = Guid.Parse("11111111-1111-1111-1111-111111111106");

    private static readonly Guid ProductWaiteInicianteId = Guid.Parse("11111111-1111-1111-1111-111111111107");

    private static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    private static readonly Guid CustomerUserId = Guid.Parse("22222222-2222-2222-2222-222222222202");

    private static readonly Guid CouponDesconto5Id = Guid.Parse("33333333-3333-3333-3333-333333333301");

    public DevSeed(EsoteraDbContext context, IPasswordHasher passwordHasher, ILogger<DevSeed> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Iniciando seed de dados de desenvolvimento...");

        await SeedCategoriesAsync();
        await SeedProductsAsync();
        await EnsureWaiteInicianteProductAsync();
        await SeedUsersAsync();
        await SeedCouponsAsync();
        await SeedStoreSettingsAsync();

        _logger.LogInformation("Seed de dados de desenvolvimento concluído.");
    }

    private async Task SeedCategoriesAsync()
    {
        if (await _context.Categories.AnyAsync())
            return;

        var categories = new[]
        {
            new Category { Id = CategoryTarosId, Name = "Tarôs", Slug = "taros" },
            new Category { Id = CategoryLivrosId, Name = "Livros", Slug = "livros" },
            new Category { Id = CategoryAcessoriosId, Name = "Acessórios", Slug = "acessorios" }
        };

        _context.Categories.AddRange(categories);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Categorias criadas.");
    }

    private async Task SeedProductsAsync()
    {
        var now = DateTime.UtcNow;

        var products = new[]
        {
            new Product
            {
                Id = ProductWaiteTradId,
                Slug = "taro-rider-waite-tradicional",
                Name = "Tarô Rider-Waite Tradicional",
                ShortDescription = "O clássico tarô de Arthur Edward Waite com ilustrações de Pamela Colman Smith",
                Description = "O Tarô Rider-Waite é um dos baralhos de tarô mais populares e influentes do mundo. Criado em 1909 por Arthur Edward Waite e ilustrado por Pamela Colman Smith, este baralho revolucionou a leitura de tarô ao incluir cenas ilustrativas em todas as 78 cartas, não apenas nos Arcanos Maiores.",
                Price = 89.90m,
                CategoryId = CategoryTarosId,
                FeaturesJson = "[\"78 cartas\",\"Ilustrações clássicas\",\"Acabamento premium\",\"Caixa rígida\"]",
                PackageContentsJson = "[\"78 cartas de tarô\",\"Livreto explicativo\",\"Caixa de apresentação\"]",
                VariationsJson = null,
                IsFeatured = false,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/waite-tradicional-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now },
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/waite-tradicional-2.jpg", SortOrder = 2, IsPrimary = false, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductWaiteInicianteId,
                Slug = "rider-waite-taro-esotera-para-iniciante",
                Name = "Rider Waite Tarô Esotera para Iniciante com 78 Cartas, Ilustrações e Explicações nas Cartas",
                ShortDescription = "Edição para iniciantes com ilustrações e explicações nas cartas.",
                Description = "Rider Waite Tarô Esotera para iniciantes com 78 cartas ilustradas e explicativas. Escolha entre somente o tarô ou o kit com livro.",
                Price = 54.90m,
                CategoryId = CategoryTarosId,
                FeaturesJson = "[\"78 cartas\",\"Ilustrações e explicações nas cartas\",\"Ideal para iniciantes\"]",
                PackageContentsJson = "[\"78 cartas ilustradas\"]",
                VariationsJson = """[{"id":"var-somente-taro","name":"Somente Tarô","price":54.90,"isAvailable":true,"sku":"SKU-WAITE-TAROT"},{"id":"var-taro-livro","name":"Tarô + Livro","price":79.90,"isAvailable":true,"sku":"SKU-WAITE-KIT"},{"id":"var-somente-livro","name":"Somente Livro","price":0,"isAvailable":false,"sku":"SKU-WAITE-LIVRO"}]""",
                IsFeatured = true,
                IsAvailable = true,
                IsDemo = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/waite-iniciante.png", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductWaitePocketId,
                Slug = "taro-rider-waite-pocket",
                Name = "Tarô Rider-Waite Pocket",
                ShortDescription = "Versão compacta do clássico Rider-Waite, ideal para viagens",
                Description = "Versão pocket do tradicional Tarô Rider-Waite, perfeito para levar em viagens ou para quem prefere cartas menores. Mantém todas as características do original em um formato mais compacto.",
                Price = 59.90m,
                CategoryId = CategoryTarosId,
                FeaturesJson = "[\"78 cartas\",\"Tamanho compacto\",\"Fácil de transportar\"]",
                PackageContentsJson = "[\"78 cartas de tarô\",\"Livreto resumido\"]",
                IsFeatured = false,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/waite-pocket-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductCrowleyId,
                Slug = "taro-de-crowley",
                Name = "Tarô de Crowley (Thoth)",
                ShortDescription = "O poderoso tarô de Aleister Crowley com arte de Lady Frieda Harris",
                Description = "O Tarô de Thoth foi criado por Aleister Crowley e pintado por Lady Frieda Harris entre 1938 e 1943. É considerado um dos baralhos mais complexos e esotéricos, incorporando simbolismo da Cabala, astrologia, alquimia e outras tradições místicas.",
                Price = 129.90m,
                CategoryId = CategoryTarosId,
                FeaturesJson = "[\"78 cartas\",\"Arte detalhada\",\"Simbolismo profundo\",\"Edição de luxo\"]",
                PackageContentsJson = "[\"78 cartas de tarô\",\"Livro completo de interpretações\",\"Caixa premium\"]",
                VariationsJson = null,
                IsFeatured = true,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/crowley-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now },
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/crowley-2.jpg", SortOrder = 2, IsPrimary = false, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductMarselhaId,
                Slug = "taro-de-marselha-restaurado",
                Name = "Tarô de Marselha Restaurado",
                ShortDescription = "Edição restaurada do histórico Tarô de Marselha",
                Description = "Esta edição restaurada do Tarô de Marselha traz de volta a beleza das gravuras originais do século XVIII. Com cores vibrantes e detalhes preservados, é uma peça de colecionador e uma ferramenta poderosa para leituras tradicionais.",
                Price = 109.90m,
                CategoryId = CategoryTarosId,
                FeaturesJson = "[\"78 cartas\",\"Restauração histórica\",\"Cores originais\",\"Numeração tradicional\"]",
                PackageContentsJson = "[\"78 cartas de tarô\",\"Guia histórico\",\"Caixa colecionador\"]",
                IsFeatured = false,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/marselha-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductLivro78Id,
                Slug = "78-graus-de-sabedoria",
                Name = "78 Graus de Sabedoria",
                ShortDescription = "O guia definitivo para interpretação do Tarô por Rachel Pollack",
                Description = "Considerado a 'bíblia' do Tarô moderno, este livro de Rachel Pollack oferece interpretações profundas de cada uma das 78 cartas, combinando simbolismo tradicional com insights psicológicos contemporâneos.",
                Price = 79.90m,
                CategoryId = CategoryLivrosId,
                FeaturesJson = "[\"Capa dura\",\"Ilustrações coloridas\",\"400+ páginas\"]",
                PackageContentsJson = "[\"Livro 78 Graus de Sabedoria\"]",
                IsFeatured = true,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/78-graus-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now }
                }
            },
            new Product
            {
                Id = ProductToadaId,
                Slug = "toalha-leitura-veludo",
                Name = "Toalha de Leitura Veludo",
                ShortDescription = "Toalha em veludo bordado para suas leituras de tarô",
                Description = "Toalha de veludo de alta qualidade, perfeita para criar um ambiente sagrado para suas leituras de tarô. Com bordados místicos e tamanho ideal para espalhamento de cartas.",
                Price = 49.90m,
                CategoryId = CategoryAcessoriosId,
                FeaturesJson = "[\"Veludo premium\",\"Bordados à mão\",\"60x60cm\",\"Lavável\"]",
                PackageContentsJson = "[\"Toalha de veludo\",\"Saquinho de proteção\"]",
                VariationsJson = null,
                IsFeatured = false,
                IsAvailable = true,
                IsDemo = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new() { Id = Guid.NewGuid(), SecureUrl = "/images/products/toalha-1.jpg", SortOrder = 1, IsPrimary = true, CreatedAtUtc = now }
                }
            }
        };

        var existingIds = await _context.Products.Select(p => p.Id).ToListAsync();
        var toAdd = products.Where(p => !existingIds.Contains(p.Id)).ToArray();
        if (toAdd.Length == 0)
            return;

        _context.Products.AddRange(toAdd);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Produtos de desenvolvimento criados: {Count}.", toAdd.Length);
    }

    /// <summary>
    /// Garante o produto de referência com variações e preços confirmados (idempotente).
    /// </summary>
    private async Task EnsureWaiteInicianteProductAsync()
    {
        const string slug = "rider-waite-taro-esotera-para-iniciante";
        var now = DateTime.UtcNow;
        var variationsJson =
            """[{"id":"var-somente-taro","name":"Somente Tarô","price":54.90,"isAvailable":true,"sku":"SKU-WAITE-TAROT"},{"id":"var-taro-livro","name":"Tarô + Livro","price":79.90,"isAvailable":true,"sku":"SKU-WAITE-KIT"},{"id":"var-somente-livro","name":"Somente Livro","price":0,"isAvailable":false,"sku":"SKU-WAITE-LIVRO"}]""";

        var categoryId = await _context.Categories
            .Where(c => c.Slug == "taros")
            .Select(c => c.Id)
            .FirstOrDefaultAsync();
        if (categoryId == Guid.Empty)
            categoryId = CategoryTarosId;

        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == slug || p.Id == ProductWaiteInicianteId);

        if (product == null)
        {
            product = new Product
            {
                Id = ProductWaiteInicianteId,
                Slug = slug,
                CategoryId = categoryId,
                CreatedAtUtc = now,
                Images = new List<ProductImage>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        SecureUrl = "/images/products/waite-iniciante.png",
                        SortOrder = 1,
                        IsPrimary = true,
                        CreatedAtUtc = now
                    }
                }
            };
            _context.Products.Add(product);
        }

        product.Slug = slug;
        product.Name = "Rider Waite Tarô Esotera para Iniciante com 78 Cartas, Ilustrações e Explicações nas Cartas";
        product.ShortDescription = "Edição para iniciantes com ilustrações e explicações nas cartas.";
        product.Description = "Rider Waite Tarô Esotera para iniciantes com 78 cartas ilustradas e explicativas. Escolha entre somente o tarô ou o kit com livro.";
        product.Price = 54.90m;
        product.CategoryId = categoryId;
        product.FeaturesJson = "[\"78 cartas\",\"Ilustrações e explicações nas cartas\",\"Ideal para iniciantes\"]";
        product.PackageContentsJson = "[\"78 cartas ilustradas\"]";
        product.VariationsJson = variationsJson;
        product.IsFeatured = true;
        product.IsAvailable = true;
        product.IsDemo = false;
        product.IsArchived = false;
        product.UpdatedAtUtc = now;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Produto de referência Waite Iniciante sincronizado.");
    }

    private async Task SeedUsersAsync()
    {
        if (await _context.Users.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var passwordHash = _passwordHasher.Hash("demo123");

        var users = new[]
        {
            new User
            {
                Id = AdminUserId,
                Name = "Admin Esotera",
                Email = "admin@esotera.demo",
                PasswordHash = passwordHash,
                Cpf = "12345678901",
                Role = UserRole.Admin,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new User
            {
                Id = CustomerUserId,
                Name = "Cliente Teste",
                Email = "cliente@esotera.demo",
                PasswordHash = passwordHash,
                Cpf = "98765432100",
                Phone = "11999998888",
                Role = UserRole.Customer,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Addresses = new List<Address>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Cep = "01310100",
                        Street = "Avenida Paulista",
                        Number = "1000",
                        Complement = "Apto 123",
                        Neighborhood = "Bela Vista",
                        City = "São Paulo",
                        State = "SP",
                        IsPrimary = true,
                        CreatedAtUtc = now
                    }
                }
            }
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Usuários criados.");
    }

    private async Task SeedCouponsAsync()
    {
        const string code = "DESCONTO5";
        if (await _context.Coupons.AnyAsync(c => c.Code == code))
            return;

        var now = DateTime.UtcNow;
        var coupon = new Coupon
        {
            Id = CouponDesconto5Id,
            Code = code,
            DiscountAmount = 5.00m,
            MinPurchase = 30.00m,
            AppliesToShipping = false,
            OneUsePerCustomer = true,
            MaxTotalUses = null,
            IsActive = true,
            IsArchived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cupons criados.");
    }

    private async Task SeedStoreSettingsAsync()
    {
        if (await _context.StoreSettings.AnyAsync(s => s.Id == 1))
            return;

        var settings = new StoreSettings
        {
            Id = 1,
            StoreName = "Esotera",
            FreeShippingMin = 99.90m,
            FreeShippingStatesCsv = "SP,RJ,MG,ES,PR,SC,RS",
            J3Price = 12.00m,
            J3CutoffHour = 12,
#pragma warning disable CS0618
            CouponDiscount = 5.00m,
            CouponMinPurchase = 30.00m,
#pragma warning restore CS0618
            ShippingSubsidyEnabled = false,
            ShippingSubsidyAmount = 10.00m,
            ShippingOriginCep = "08061420",
            PackageLengthCm = 16m,
            PackageWidthCm = 11m,
            PackageHeightCm = 6m,
            PackageWeightGrams = 400,
            MelhorEnvioQuoteEnabled = false,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.StoreSettings.Add(settings);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Configurações da loja criadas.");
    }
}
