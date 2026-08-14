using System.Data;
using Esotera.Application.Interfaces;
using Esotera.Domain.Entities;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esotera.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"EsoteraTestDb_{Guid.NewGuid()}";
    private SqliteConnection? _sqliteConnection;

    /// <summary>
    /// SQLite in-memory relacional. Autoritativo para webhook MP → J3Fulfillment Pending.
    /// EF InMemory não representa esse fluxo (1:1 + concurrency token).
    /// </summary>
    protected virtual bool UseSqliteDatabase => false;

    /// <summary>Interceptores extras (ex.: falha forçada no insert de J3Fulfillment).</summary>
    protected virtual IEnumerable<IInterceptor> ExtraDbInterceptors => [];

    /// <summary>
    /// Default true: testes de cotação/pedido com J3 real via FakeJ3Client.
    /// Default da app de produção permanece J3_ENABLED=false.
    /// Override em factories derivadas para testar gate desligado.
    /// </summary>
    protected virtual bool J3EnabledForTests => true;

    /// <summary>
    /// Default false (igual produção). Claim/mutations futuras; Pending não depende desta flag.
    /// </summary>
    protected virtual bool J3FulfillmentEnabledForTests => false;

    /// <summary>
    /// Com J3 habilitado nos testes, exige preço positivo (fail-closed).
    /// 1299 = fake explícito só em testes — NÃO é default de produção.
    /// Null = não setar (ausente); 0 = inválido explícito.
    /// </summary>
    protected virtual int? J3StandardPriceCentsForTests => 1299;

    /// <summary>URL GraphQL fake (nunca j3tms.com.br). Null = omitir (config inválida).</summary>
    protected virtual string? J3GraphQlUrlForTests => "http://localhost/j3-graphql-test/";

    /// <summary>Token fake. Null = omitir.</summary>
    protected virtual string? J3TokenForTests => "fake-j3-token-for-tests";

    /// <summary>Company group. Null = omitir (string vazia explícita).</summary>
    protected virtual string? J3CompanyGroupCodeForTests => "J3";

    /// <summary>Seller ID fake de teste. Null = omitir.</summary>
    protected virtual string? J3SellerIdForTests => null;

    /// <summary>Seller information ID fake de teste. Null = omitir.</summary>
    protected virtual string? J3SellerInformationIdForTests => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var j3Enabled = J3EnabledForTests;
        var j3FulfillmentEnabled = J3FulfillmentEnabledForTests;
        var j3PriceCents = J3StandardPriceCentsForTests;
        var j3Url = J3GraphQlUrlForTests;
        var j3Token = J3TokenForTests;
        var j3Group = J3CompanyGroupCodeForTests;
        var j3SellerId = J3SellerIdForTests;
        var j3SellerInfo = J3SellerInformationIdForTests;
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                // Tokens fictícios apenas para IsConfigured nos testes — nunca credenciais reais.
                ["MERCADO_PAGO_ACCESS_TOKEN"] = "test-access-token-for-unit-tests-only",
                ["MERCADO_PAGO_ENVIRONMENT"] = "Test",
                ["MercadoPago__Environment"] = "Test",
                ["MERCADO_PAGO_SANDBOX_PIX_ENABLED"] = "true",
                ["MERCADO_PAGO_SANDBOX_PIX_AMOUNT"] = "50.00",
                ["MERCADO_PAGO_WEBHOOK_SECRET"] = "test-webhook-secret",
                ["PUBLIC_API_BASE_URL"] = "http://localhost",
                // Melhor Envio OAuth Sandbox — valores fictícios de teste.
                ["MELHOR_ENVIO_ENABLED"] = "true",
                ["MELHOR_ENVIO_ENVIRONMENT"] = "sandbox",
                ["MELHOR_ENVIO_CLIENT_ID"] = "100001",
                ["MELHOR_ENVIO_CLIENT_SECRET"] = "test-me-client-secret-not-real",
                ["MELHOR_ENVIO_REDIRECT_URI"] = "http://localhost/api/integrations/melhor-envio/callback",
                ["MELHOR_ENVIO_USER_AGENT"] = "Esotera Test (test@esotera.demo)",
                ["FRONTEND_BASE_URL"] = "https://esotera.vercel.app",
                // 32 bytes zero — apenas testes.
                ["INTEGRATIONS_ENCRYPTION_KEY"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                // App default é false; testes que esperam J3 usam true (este default da factory).
                ["J3_ENABLED"] = j3Enabled ? "true" : "false",
                ["J3:Enabled"] = j3Enabled ? "true" : "false",
                ["J3_FULFILLMENT_ENABLED"] = j3FulfillmentEnabled ? "true" : "false",
                ["J3:FulfillmentEnabled"] = j3FulfillmentEnabled ? "true" : "false"
            };

            if (j3PriceCents.HasValue)
            {
                // Fake de teste explícito; produção deixa J3_STANDARD_PRICE_CENTS vazio (default 0).
                values["J3_STANDARD_PRICE_CENTS"] = j3PriceCents.Value.ToString();
                values["J3:StandardPriceCents"] = j3PriceCents.Value.ToString();
            }

            if (j3Url is not null)
            {
                values["J3_GRAPHQL_URL"] = j3Url;
                values["J3:GraphQlUrl"] = j3Url;
            }

            if (j3Token is not null)
            {
                values["J3_TOKEN"] = j3Token;
                values["J3:Token"] = j3Token;
            }

            if (j3Group is not null)
            {
                values["J3_COMPANY_GROUP_CODE"] = j3Group;
                values["J3:CompanyGroupCode"] = j3Group;
            }

            if (j3SellerId is not null)
            {
                values["J3_SELLER_ID"] = j3SellerId;
                values["J3:SellerId"] = j3SellerId;
            }

            if (j3SellerInfo is not null)
            {
                values["J3_SELLER_INFORMATION_ID"] = j3SellerInfo;
                values["J3:SellerInformationId"] = j3SellerInfo;
            }

            config.AddInMemoryCollection(values);
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
                if (UseSqliteDatabase)
                {
                    // Keep-alive separado: cada DbContext abre a própria conexão no mesmo
                    // cache compartilhado. Reusar UM SqliteConnection em todos os scopes
                    // quebra SaveChanges (transaction do CreateOrder + RowVersion → 0 rows).
                    var cs = $"DataSource=file:{_dbName}?mode=memory&cache=shared";
                    _sqliteConnection ??= new SqliteConnection(cs);
                    if (_sqliteConnection.State != ConnectionState.Open)
                        _sqliteConnection.Open();
                    options.UseSqlite(cs);
                }
                else
                {
                    options.UseInMemoryDatabase(_dbName);
                }

                foreach (var interceptor in ExtraDbInterceptors)
                    options.AddInterceptors(interceptor);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _sqliteConnection?.Dispose();
        base.Dispose(disposing);
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

            // Habilita cotação ME + OAuth fake para regressão de CreateOrder (flag inicia off no seed).
            ShippingTestHelpers.EnableMelhorEnvioQuoteAsync(
                    host.Services,
                    enabled: true,
                    withOAuthConnection: true)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred seeding the database. Error: {ex.Message}");
        }

        return host;
    }
}

/// <summary>Factory de testes com J3_ENABLED=false (default da app).</summary>
public sealed class J3DisabledWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => false;
}

/// <summary>J3 + fulfillment habilitados (Pending/claim). Sem HTTP J3 mutativo.</summary>
public class J3FulfillmentEnabledWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => true;
    protected override bool J3FulfillmentEnabledForTests => true;
    protected override string? J3SellerIdForTests => "test-seller-id";
    protected override string? J3SellerInformationIdForTests => "test-seller-info";
}

/// <summary>SQLite relacional; J3 quote on; fulfillment flag false (Pending não depende da flag).</summary>
public class SqliteWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool UseSqliteDatabase => true;
}

/// <summary>
/// SQLite: falha local no SaveChanges do insert J3Fulfillment (depois do Save do Order).
/// Sem HTTP J3. Prova rollback atômico.
/// </summary>
public sealed class SqliteJ3FulfillmentInsertFailsWebApplicationFactory : SqliteWebApplicationFactory
{
    protected override IEnumerable<IInterceptor> ExtraDbInterceptors =>
        [new FailOnJ3FulfillmentInsertInterceptor()];
}

/// <summary>SQLite relacional + fulfillment on — E2E processor manual. Webhook ainda não chama processor.</summary>
public sealed class SqliteJ3FulfillmentEnabledWebApplicationFactory : J3FulfillmentEnabledWebApplicationFactory
{
    protected override bool UseSqliteDatabase => true;
}

/// <summary>Fulfillment on, quote off — pedidos históricos J3 já pagos.</summary>
public sealed class J3FulfillmentOnlyWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => false;
    protected override bool J3FulfillmentEnabledForTests => true;
    protected override string? J3SellerIdForTests => "test-seller-id";
    protected override string? J3SellerInformationIdForTests => "test-seller-info";
}

/// <summary>Enabled=true mas preço ausente (não seta J3_STANDARD_PRICE_CENTS) — fail-closed.</summary>
public sealed class J3EnabledMissingPriceWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => true;
    protected override int? J3StandardPriceCentsForTests => null;
}

/// <summary>Enabled=true mas preço 0 — fail-closed.</summary>
public sealed class J3EnabledZeroPriceWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => true;
    protected override int? J3StandardPriceCentsForTests => 0;
}

/// <summary>Enabled=true mas sem URL GraphQL — config inválida, sem chamada client.</summary>
public sealed class J3EnabledMissingUrlWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => true;
    protected override string? J3GraphQlUrlForTests => null;
}

/// <summary>Enabled=true mas sem token — config inválida.</summary>
public sealed class J3EnabledMissingTokenWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool J3EnabledForTests => true;
    protected override string? J3TokenForTests => null;
}
