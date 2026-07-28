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

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ISimulatedShippingService, SimulatedShippingService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        if (isTestEnvironment)
        {
            services.AddSingleton<FakeProductImageStorage>();
            services.AddSingleton<IProductImageStorage>(sp => sp.GetRequiredService<FakeProductImageStorage>());
        }
        else
        {
            var cloudinary = BindCloudinary(configuration);
            if (cloudinary.IsConfigured)
                services.AddScoped<IProductImageStorage, CloudinaryProductImageStorage>();
            else
                services.AddScoped<IProductImageStorage, UnconfiguredProductImageStorage>();
        }

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IStoreSettingsService, StoreSettingsService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminQueryService, AdminQueryService>();

        services.AddScoped<DevSeed>();

        return services;
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
