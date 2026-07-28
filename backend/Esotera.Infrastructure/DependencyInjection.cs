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
            if (int.TryParse(configuration["EMAIL_SMTP_PORT"] ?? configuration["Email:SmtpPort"], out var port))
                options.SmtpPort = port;
            var ssl = ParseBool(configuration["EMAIL_SMTP_USE_SSL"] ?? configuration["Email:SmtpUseSsl"]);
            if (ssl.HasValue) options.SmtpUseSsl = ssl.Value;
        });

        services.Configure<MercadoPagoOptions>(options =>
        {
            configuration.GetSection(MercadoPagoOptions.SectionName).Bind(options);
            options.AccessToken = FirstNonEmpty(
                options.AccessToken,
                configuration["MERCADO_PAGO_ACCESS_TOKEN"],
                configuration["MercadoPago:AccessToken"]);
            options.WebhookSecret = FirstNonEmpty(
                options.WebhookSecret,
                configuration["MERCADO_PAGO_WEBHOOK_SECRET"],
                configuration["MercadoPago:WebhookSecret"]);
            options.Environment = FirstNonEmpty(
                options.Environment,
                configuration["MERCADO_PAGO_ENVIRONMENT"],
                configuration["MercadoPago:Environment"]) ?? "test";
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ISimulatedShippingService, SimulatedShippingService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        if (isTestEnvironment)
        {
            services.AddSingleton<FakeProductImageStorage>();
            services.AddSingleton<IProductImageStorage>(sp => sp.GetRequiredService<FakeProductImageStorage>());
            services.AddSingleton<CapturingEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
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

        services.AddScoped<DevSeed>();

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
}
