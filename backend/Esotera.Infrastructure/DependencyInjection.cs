using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Esotera.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? environment = null)
    {
        var isTestEnvironment = environment?.EnvironmentName == "Testing";

        if (!isTestEnvironment)
        {
            var connectionString = configuration.GetConnectionString("Default");

            services.AddDbContext<EsoteraDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(EsoteraDbContext).Assembly.FullName);
                });
            });
        }

        BindCloudinaryOptions(services, configuration);

        services.Configure<EmailOptions>(options =>
        {
            configuration.GetSection(EmailOptions.SectionName).Bind(options);
            options.Enabled = ParseBool(configuration["Email:Enabled"] ?? configuration["EMAIL_ENABLED"]) ?? options.Enabled;
            options.SmtpHost = FirstNonEmpty(options.SmtpHost, configuration["EMAIL_SMTP_HOST"], configuration["Email:SmtpHost"]);
            options.SmtpUser = FirstNonEmpty(options.SmtpUser, configuration["EMAIL_SMTP_USER"], configuration["Email:SmtpUser"]);
            options.SmtpPassword = FirstNonEmpty(options.SmtpPassword, configuration["EMAIL_SMTP_PASSWORD"], configuration["Email:SmtpPassword"]);
            options.FromAddress = FirstNonEmpty(options.FromAddress, configuration["EMAIL_FROM_ADDRESS"], configuration["Email:FromAddress"])
                ?? "esoteralivraria1@gmail.com";
            options.FromName = FirstNonEmpty(options.FromName, configuration["EMAIL_FROM_NAME"], configuration["Email:FromName"])
                ?? "Esotera";
            options.FrontendBaseUrl = FirstNonEmpty(options.FrontendBaseUrl, configuration["FRONTEND_BASE_URL"], configuration["Email:FrontendBaseUrl"]);
            options.AdminNotifyEmail = FirstNonEmpty(
                options.AdminNotifyEmail,
                configuration["EMAIL_ADMIN_NOTIFY"],
                configuration["Email:AdminNotifyEmail"]);
            if (int.TryParse(configuration["EMAIL_SMTP_PORT"] ?? configuration["Email:SmtpPort"], out var port))
                options.SmtpPort = port;
            var ssl = ParseBool(configuration["EMAIL_SMTP_USE_SSL"] ?? configuration["Email:SmtpUseSsl"]);
            if (ssl.HasValue) options.SmtpUseSsl = ssl.Value;
        });

        services.Configure<MercadoPagoOptions>(options =>
        {
            configuration.GetSection(MercadoPagoOptions.SectionName).Bind(options);
            var (accessToken, accessTokenSource) = FirstNonEmptyWithSource(
                (options.AccessToken, "MercadoPago:AccessToken"),
                (configuration["MERCADO_PAGO_ACCESS_TOKEN"], "MERCADO_PAGO_ACCESS_TOKEN"),
                (configuration["MercadoPago:AccessToken"], "MercadoPago:AccessToken"));
            options.AccessToken = accessToken;
            options.AccessTokenSource = accessTokenSource;
            options.WebhookSecret = FirstNonEmpty(
                options.WebhookSecret,
                configuration["MERCADO_PAGO_WEBHOOK_SECRET"],
                configuration["MercadoPago:WebhookSecret"]);
            // Variáveis de ambiente têm precedência sobre appsettings (ex.: Test → Production no Render).
            options.Environment = FirstNonEmpty(
                configuration["MERCADO_PAGO_ENVIRONMENT"],
                configuration["MercadoPago:Environment"],
                options.Environment) ?? "Test";
            options.EnvironmentKind = MercadoPagoOptions.ParseEnvironmentKind(options.Environment);
            options.NotificationUrl = FirstNonEmpty(
                options.NotificationUrl,
                configuration["MERCADO_PAGO_NOTIFICATION_URL"],
                configuration["MercadoPago:NotificationUrl"]);
            options.PublicApiBaseUrl = FirstNonEmpty(
                options.PublicApiBaseUrl,
                configuration["PUBLIC_API_BASE_URL"],
                configuration["MercadoPago:PublicApiBaseUrl"]);

            var sandboxEnabledRaw = FirstNonEmpty(
                configuration["MERCADO_PAGO_SANDBOX_PIX_ENABLED"],
                configuration["MercadoPago:SandboxPixEnabled"]);
            var sandboxEnabled = ParseBool(sandboxEnabledRaw);
            if (sandboxEnabled.HasValue)
                options.SandboxPixEnabled = sandboxEnabled.Value;

            var amountRaw = FirstNonEmpty(
                configuration["MERCADO_PAGO_SANDBOX_PIX_AMOUNT"],
                configuration["MercadoPago:SandboxPixAmount"]);
            if (decimal.TryParse(
                    amountRaw,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sandboxAmount)
                && sandboxAmount > 0)
            {
                options.SandboxPixAmount = sandboxAmount;
            }

            // Produção nunca habilita o fluxo isolado de teste.
            if (options.IsProductionEnvironment)
                options.SandboxPixEnabled = false;
        });

        services.AddSingleton<IClock, SystemClock>();
        services.Configure<MelhorEnvioOptions>(options =>
        {
            configuration.GetSection(MelhorEnvioOptions.SectionName).Bind(options);
            options.Enabled = ParseBool(configuration["MELHOR_ENVIO_ENABLED"] ?? configuration["MelhorEnvio:Enabled"]) ?? options.Enabled;
            options.ClientId = FirstNonEmpty(options.ClientId, configuration["MELHOR_ENVIO_CLIENT_ID"], configuration["MelhorEnvio:ClientId"]);
            options.ClientSecret = FirstNonEmpty(options.ClientSecret, configuration["MELHOR_ENVIO_CLIENT_SECRET"], configuration["MelhorEnvio:ClientSecret"]);
            options.Environment = FirstNonEmpty(options.Environment, configuration["MELHOR_ENVIO_ENVIRONMENT"], configuration["MelhorEnvio:Environment"])
                ?? "sandbox";
        });
        services.Configure<J3ShippingOptions>(options =>
        {
            configuration.GetSection(J3ShippingOptions.SectionName).Bind(options);
            options.Enabled = ParseBool(configuration["J3_ENABLED"] ?? configuration["J3:Enabled"]) ?? options.Enabled;
            options.ApiUrl = FirstNonEmpty(options.ApiUrl, configuration["J3_API_URL"], configuration["J3:ApiUrl"]);
            options.ApiToken = FirstNonEmpty(options.ApiToken, configuration["J3_API_TOKEN"], configuration["J3:ApiToken"]);
        });
        services.AddScoped<SimulatedShippingService>();
        services.AddScoped<ShippingQuoteService>();
        services.AddScoped<IShippingQuoteService>(sp => sp.GetRequiredService<ShippingQuoteService>());
        services.AddScoped<ISimulatedShippingService>(sp => sp.GetRequiredService<ShippingQuoteService>());
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        if (isTestEnvironment)
        {
            services.AddSingleton<FakeProductImageStorage>();
            services.AddSingleton<IProductImageStorage>(sp => sp.GetRequiredService<FakeProductImageStorage>());
            services.AddSingleton<CapturingEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
            services.AddSingleton<FakeMercadoPagoClient>();
            services.AddSingleton<IMercadoPagoClient>(sp => sp.GetRequiredService<FakeMercadoPagoClient>());
        }
        else
        {
            var cloudinary = BindCloudinary(configuration);
            if (cloudinary.IsConfigured)
                services.AddScoped<IProductImageStorage, CloudinaryProductImageStorage>();
            else
                services.AddScoped<IProductImageStorage, UnconfiguredProductImageStorage>();

            services.AddScoped<IEmailSender>(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value;
                if (opts.IsSmtpConfigured)
                    return ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp);
                return ActivatorUtilities.CreateInstance<NullEmailSender>(sp);
            });

            services.AddHttpClient<IMercadoPagoClient, MercadoPagoHttpClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.mercadopago.com/");
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IStoreSettingsService, StoreSettingsService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminQueryService, AdminQueryService>();
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<DevSeed>();
        services.AddScoped<AdminBootstrap>();
        services.AddScoped<CatalogBootstrap>();

        return services;
    }

    private static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (bool.TryParse(value, out var b)) return b;
        if (value is "1" or "yes" or "YES") return true;
        if (value is "0" or "no" or "NO") return false;
        return null;
    }

    private static void BindCloudinaryOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CloudinaryOptions>(options =>
        {
            var bound = BindCloudinary(configuration);
            options.CloudName = bound.CloudName;
            options.ApiKey = bound.ApiKey;
            options.ApiSecret = bound.ApiSecret;
            options.ProductsFolder = bound.ProductsFolder;
        });
    }

    private static CloudinaryOptions BindCloudinary(IConfiguration configuration)
    {
        var options = new CloudinaryOptions();
        configuration.GetSection(CloudinaryOptions.SectionName).Bind(options);

        options.CloudName = FirstNonEmpty(
            options.CloudName,
            configuration["CLOUDINARY_CLOUD_NAME"],
            configuration["Cloudinary:CloudName"]) ?? string.Empty;

        options.ApiKey = FirstNonEmpty(
            options.ApiKey,
            configuration["CLOUDINARY_API_KEY"],
            configuration["Cloudinary:ApiKey"]) ?? string.Empty;

        options.ApiSecret = FirstNonEmpty(
            options.ApiSecret,
            configuration["CLOUDINARY_API_SECRET"],
            configuration["Cloudinary:ApiSecret"]) ?? string.Empty;

        options.ProductsFolder = FirstNonEmpty(
            options.ProductsFolder,
            configuration["CLOUDINARY_PRODUCTS_FOLDER"],
            configuration["Cloudinary:ProductsFolder"]) ?? "esotera/products";

        var cloudinaryUrl = configuration["CLOUDINARY_URL"];
        if (!options.IsConfigured && !string.IsNullOrWhiteSpace(cloudinaryUrl))
        {
            // Formato: cloudinary://<api_key>:<api_secret>@<cloud_name>
            try
            {
                var uri = new Uri(cloudinaryUrl);
                if (uri.Scheme.Equals("cloudinary", StringComparison.OrdinalIgnoreCase))
                {
                    options.CloudName = uri.Host;
                    options.ApiKey = Uri.UnescapeDataString(uri.UserInfo.Split(':')[0]);
                    options.ApiSecret = Uri.UnescapeDataString(uri.UserInfo.Split(':')[1]);
                }
            }
            catch
            {
                // Ignora URL inválida; IsConfigured permanece false
            }
        }

        return options;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Primeiro valor não vazio, já com Trim(). A fonte indica a chave de configuração usada.
    /// </summary>
    private static (string? Value, string? Source) FirstNonEmptyWithSource(
        params (string? Value, string Source)[] candidates)
    {
        foreach (var (value, source) in candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            return (value.Trim(), source);
        }

        return (null, null);
    }
}
