using Esotera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esotera.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"EsoteraTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Tokens fictícios apenas para IsConfigured nos testes — nunca credenciais reais.
                ["MERCADO_PAGO_ACCESS_TOKEN"] = "test-access-token-for-unit-tests-only",
                ["MERCADO_PAGO_ENVIRONMENT"] = "test",
                ["MERCADO_PAGO_WEBHOOK_SECRET"] = "test-webhook-secret",
                ["PUBLIC_API_BASE_URL"] = "http://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<EsoteraDbContext>) ||
                           d.ServiceType == typeof(EsoteraDbContext) ||
                           (d.ServiceType.IsGenericType && 
                            d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<EsoteraDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<EsoteraDbContext>();

        db.Database.EnsureCreated();

        try
        {
            var catalog = services.GetRequiredService<CatalogBootstrap>();
            catalog.RunAsync().GetAwaiter().GetResult();

            var seeder = services.GetRequiredService<DevSeed>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred seeding the database. Error: {ex.Message}");
        }

        return host;
    }
}
