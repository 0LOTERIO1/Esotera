using Esotera.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esotera.Tests;

public class CatalogBootstrapTests
{
    [Fact]
    public async Task EmptyDatabase_CreatesCategoriesAndReferenceProduct()
    {
        var options = new DbContextOptionsBuilder<EsoteraDbContext>()
            .UseInMemoryDatabase($"catalog_{Guid.NewGuid():N}")
            .Options;
        await using var db = new EsoteraDbContext(options);
        var bootstrap = new CatalogBootstrap(db, NullLogger<CatalogBootstrap>.Instance);

        await bootstrap.RunAsync();
        await bootstrap.RunAsync(); // idempotente

        (await db.Categories.CountAsync()).Should().Be(3);
        (await db.Categories.Select(c => c.Slug).OrderBy(s => s).ToListAsync())
            .Should()
            .BeEquivalentTo(["acessorios", "livros", "taros"]);

        var product = await db.Products.SingleAsync();
        product.Slug.Should().Be("rider-waite-taro-esotera-para-iniciante");
        product.IsDemo.Should().BeFalse();
        product.IsAvailable.Should().BeTrue();
    }
}
