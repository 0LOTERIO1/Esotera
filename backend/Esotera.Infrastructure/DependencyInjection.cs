using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
            if (int.TryParse(
                    configuration["EMAIL_SMTP_TIMEOUT_SECONDS"] ?? configuration["Email:SmtpTimeoutSeconds"],
                    out var smtpTimeout)
                && smtpTimeout > 0)
            {
                options.SmtpTimeoutSeconds = Math.Clamp(smtpTimeout, 3, 60);
            }
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
            options.Environment = FirstNonEmpty(
                configuration["MELHOR_ENVIO_ENVIRONMENT"],
                configuration["MelhorEnvio:Environment"],
                options.Environment) ?? "sandbox";
            options.BaseUrl = FirstNonEmpty(
                options.BaseUrl,
                configuration["MELHOR_ENVIO_BASE_URL"],
                configuration["MelhorEnvio:BaseUrl"]);
            options.RedirectUri = FirstNonEmpty(
                options.RedirectUri,
                configuration["MELHOR_ENVIO_REDIRECT_URI"],
                configuration["MelhorEnvio:RedirectUri"]);
            options.UserAgent = FirstNonEmpty(
                options.UserAgent,
                configuration["MELHOR_ENVIO_USER_AGENT"],
                configuration["MelhorEnvio:UserAgent"]);
            options.FrontendBaseUrl = FirstNonEmpty(
                options.FrontendBaseUrl,
                configuration["FRONTEND_BASE_URL"],
                configuration["MelhorEnvio:FrontendBaseUrl"],
                configuration["Email:FrontendBaseUrl"]);
            options.AutoCreateCartShipment = ParseBool(
                configuration["MELHOR_ENVIO_AUTO_CREATE_CART_SHIPMENT"]
                ?? configuration["MelhorEnvio:AutoCreateCartShipment"])
                ?? options.AutoCreateCartShipment;
            // Reservado: nenhum código desta fase lê AutoPurchaseLabel.
            options.AutoPurchaseLabel = ParseBool(
                configuration["MELHOR_ENVIO_AUTO_PURCHASE_LABEL"]
                ?? configuration["MelhorEnvio:AutoPurchaseLabel"])
                ?? options.AutoPurchaseLabel;
        });
        services.Configure<MelhorEnvioSenderOptions>(options =>
        {
            configuration.GetSection(MelhorEnvioSenderOptions.SectionName).Bind(options);
            options.Name = FirstNonEmpty(options.Name, configuration["MELHOR_ENVIO_FROM_NAME"]);
            options.Email = FirstNonEmpty(options.Email, configuration["MELHOR_ENVIO_FROM_EMAIL"]);
            options.Phone = FirstNonEmpty(options.Phone, configuration["MELHOR_ENVIO_FROM_PHONE"]);
            options.CompanyDocument = FirstNonEmpty(
                options.CompanyDocument,
                configuration["MELHOR_ENVIO_FROM_COMPANY_DOCUMENT"]);
            options.StateRegister = FirstNonEmpty(
                options.StateRegister,
                configuration["MELHOR_ENVIO_FROM_STATE_REGISTER"]);
            options.EconomicActivityCode = FirstNonEmpty(
                options.EconomicActivityCode,
                configuration["MELHOR_ENVIO_FROM_ECONOMIC_ACTIVITY_CODE"]);
            options.Address = FirstNonEmpty(options.Address, configuration["MELHOR_ENVIO_FROM_ADDRESS"]);
            options.Number = FirstNonEmpty(options.Number, configuration["MELHOR_ENVIO_FROM_NUMBER"]);
            options.Complement = FirstNonEmpty(
                options.Complement,
                configuration["MELHOR_ENVIO_FROM_COMPLEMENT"]);
            options.District = FirstNonEmpty(options.District, configuration["MELHOR_ENVIO_FROM_DISTRICT"]);
            options.City = FirstNonEmpty(options.City, configuration["MELHOR_ENVIO_FROM_CITY"]);
            options.StateAbbr = FirstNonEmpty(options.StateAbbr, configuration["MELHOR_ENVIO_FROM_STATE_ABBR"]);
            options.Platform = FirstNonEmpty(options.Platform, configuration["MELHOR_ENVIO_FROM_PLATFORM"]);
        });
        services.Configure<IntegrationsEncryptionOptions>(options =>
        {
            configuration.GetSection(IntegrationsEncryptionOptions.SectionName).Bind(options);
            options.KeyBase64 = FirstNonEmpty(
                options.KeyBase64,
                configuration["INTEGRATIONS_ENCRYPTION_KEY"],
                configuration["IntegrationsEncryption:KeyBase64"]);
        });
        // J3: flat oficiais J3_* têm precedência. Section J3 / J3__* via Bind.
        // Sem aliases J3_API_URL / J3_API_TOKEN (somente J3_GRAPHQL_URL / J3_TOKEN).
        services.Configure<J3ShippingOptions>(options =>
        {
            configuration.GetSection(J3ShippingOptions.SectionName).Bind(options);
            options.Enabled = ParseBool(configuration["J3_ENABLED"] ?? configuration["J3:Enabled"]) ?? options.Enabled;
            options.FulfillmentEnabled =
                ParseBool(configuration["J3_FULFILLMENT_ENABLED"] ?? configuration["J3:FulfillmentEnabled"])
                ?? options.FulfillmentEnabled;
            options.ImportByAccessKeyEnabled =
                ParseBool(configuration["J3_IMPORT_BY_ACCESS_KEY_ENABLED"]
                    ?? configuration["J3:ImportByAccessKeyEnabled"])
                ?? options.ImportByAccessKeyEnabled;
            options.GraphQlUrl = FirstNonEmpty(
                configuration["J3_GRAPHQL_URL"],
                configuration["J3:GraphQlUrl"],
                options.GraphQlUrl);
            options.Token = FirstNonEmpty(
                configuration["J3_TOKEN"],
                configuration["J3:Token"],
                options.Token);
            options.LoginEmail = FirstNonEmpty(
                configuration["J3_LOGIN_EMAIL"],
                configuration["J3:LoginEmail"],
                options.LoginEmail);
            options.LoginPassword = FirstNonEmpty(
                configuration["J3_LOGIN_PASSWORD"],
                configuration["J3:LoginPassword"],
                options.LoginPassword);
            options.LoginUrl = FirstNonEmpty(
                configuration["J3_LOGIN_URL"],
                configuration["J3:LoginUrl"],
                options.LoginUrl) ?? options.LoginUrl;
            options.CompanyGroupCode = FirstNonEmpty(
                configuration["J3_COMPANY_GROUP_CODE"],
                configuration["J3:CompanyGroupCode"],
                options.CompanyGroupCode) ?? "J3";
            options.SellerId = FirstNonEmpty(
                configuration["J3_SELLER_ID"],
                configuration["J3:SellerId"],
                options.SellerId) ?? string.Empty;
            options.SellerInformationId = FirstNonEmpty(
                configuration["J3_SELLER_INFORMATION_ID"],
                configuration["J3:SellerInformationId"],
                options.SellerInformationId) ?? string.Empty;
            options.OriginZip = FirstNonEmpty(
                configuration["J3_ORIGIN_ZIP"],
                configuration["J3:OriginZip"],
                options.OriginZip);
            options.EmitterPhone = FirstNonEmpty(
                configuration["J3_EMITTER_PHONE"],
                configuration["J3:EmitterPhone"],
                options.EmitterPhone);
            options.Ecommerce = FirstNonEmpty(
                configuration["J3_ECOMMERCE"],
                configuration["J3:Ecommerce"],
                options.Ecommerce) ?? "Standalone";
            options.OrderPickupType = FirstNonEmpty(
                configuration["J3_ORDER_PICKUP_TYPE"],
                configuration["J3:OrderPickupType"],
                options.OrderPickupType) ?? "Standard";
            options.PackageIsFragile =
                ParseBool(configuration["J3_PACKAGE_IS_FRAGILE"] ?? configuration["J3:PackageIsFragile"])
                ?? options.PackageIsFragile;
            options.PackageIsValuable =
                ParseBool(configuration["J3_PACKAGE_IS_VALUABLE"] ?? configuration["J3:PackageIsValuable"])
                ?? options.PackageIsValuable;
            // Flat vazio não sobrescreve Bind; ausente → default 0 (inválido se Enabled).
            if (int.TryParse(
                    FirstNonEmpty(
                        configuration["J3_STANDARD_PRICE_CENTS"],
                        configuration["J3:StandardPriceCents"]),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var priceCents))
            {
                options.StandardPriceCents = priceCents;
            }
            if (int.TryParse(
                    FirstNonEmpty(
                        configuration["J3_TIMEOUT_SECONDS"],
                        configuration["J3:TimeoutSeconds"]),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var j3Timeout)
                && j3Timeout > 0)
            {
                options.TimeoutSeconds = Math.Clamp(j3Timeout, 3, 60);
            }
            if (int.TryParse(
                    FirstNonEmpty(
                        configuration["J3_PROCESSING_STALE_MINUTES"],
                        configuration["J3:ProcessingStaleMinutes"]),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var staleMinutes)
                && staleMinutes > 0)
            {
                options.ProcessingStaleMinutes = Math.Clamp(staleMinutes, 1, 24 * 60);
            }

            if (int.TryParse(
                    FirstNonEmpty(
                        configuration["J3_AUTH_RENEW_SKEW_MINUTES"],
                        configuration["J3:AuthRenewSkewMinutes"]),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var skewMinutes)
                && skewMinutes > 0)
            {
                options.AuthRenewSkewMinutes = Math.Clamp(skewMinutes, 1, 30);
            }
        });

        services.Configure<UpSellerOptions>(options =>
        {
            configuration.GetSection(UpSellerOptions.SectionName).Bind(options);
            options.StoreName = FirstNonEmpty(
                configuration["UPSELLER_STORE_NAME"],
                configuration["UpSeller:StoreName"],
                options.StoreName) ?? "Loja Padrão";
            options.WarehouseName = FirstNonEmpty(
                configuration["UPSELLER_WAREHOUSE_NAME"],
                configuration["UpSeller:WarehouseName"],
                options.WarehouseName) ?? "My Warehouse";
            options.ShippingCostMethod = FirstNonEmpty(
                configuration["UPSELLER_SHIPPING_COST_METHOD"],
                configuration["UpSeller:ShippingCostMethod"],
                options.ShippingCostMethod) ?? "2";
            options.InvoiceRequired = FirstNonEmpty(
                configuration["UPSELLER_INVOICE_REQUIRED"],
                configuration["UpSeller:InvoiceRequired"],
                options.InvoiceRequired) ?? "Não";
            options.DefaultPaymentMethod = FirstNonEmpty(
                configuration["UPSELLER_DEFAULT_PAYMENT_METHOD"],
                configuration["UpSeller:DefaultPaymentMethod"],
                options.DefaultPaymentMethod) ?? "Dinheiro";
            if (int.TryParse(
                    FirstNonEmpty(
                        configuration["UPSELLER_PACKAGE_QUANTITY"],
                        configuration["UpSeller:PackageQuantity"]),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var pkgQty)
                && pkgQty > 0)
            {
                options.PackageQuantity = Math.Clamp(pkgQty, 1, 99);
            }
        });

        services.AddScoped<SimulatedShippingService>();
        services.AddScoped<IShippingOptionsService, ShippingOptionsService>();
        services.AddScoped<ShippingQuoteService>();
        services.AddScoped<IShippingQuoteService>(sp => sp.GetRequiredService<ShippingQuoteService>());
        services.AddScoped<ISimulatedShippingService>(sp => sp.GetRequiredService<SimulatedShippingService>());
        services.AddScoped<IJ3FulfillmentService, J3FulfillmentService>();
        services.AddScoped<IMelhorEnvioShipmentLocalService, MelhorEnvioShipmentLocalService>();
        services.AddScoped<IJ3FulfillmentEligibilityService, J3FulfillmentEligibilityService>();
        services.AddScoped<IJ3FulfillmentProcessor, J3FulfillmentProcessor>();
        services.AddScoped<IJ3FulfillmentAdminQueryService, J3FulfillmentAdminQueryService>();
        services.AddScoped<IJ3FulfillmentAdminProcessService, J3FulfillmentAdminProcessService>();
        services.AddScoped<IJ3ImportOrderByAccessKeyAdminService, J3ImportOrderByAccessKeyAdminService>();
        services.AddScoped<IJ3ReconcileAdminService, J3ReconcileAdminService>();
        services.AddScoped<IJ3TrackingSyncService, J3TrackingSyncService>();
        services.AddScoped<IJ3IdentifierHydrationService, J3IdentifierHydrationService>();
        services.AddSingleton<IIntegrationsEncryptionService, IntegrationsEncryptionService>();
        services.AddScoped<IMelhorEnvioOAuthService, MelhorEnvioOAuthService>();
        services.AddScoped<IMelhorEnvioDiagnosticsService, MelhorEnvioDiagnosticsService>();
        services.AddScoped<IMelhorEnvioShipmentProcessingService, MelhorEnvioShipmentProcessingService>();
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
            services.AddSingleton<FakeMelhorEnvioOAuthClient>();
            services.AddSingleton<IMelhorEnvioOAuthClient>(sp => sp.GetRequiredService<FakeMelhorEnvioOAuthClient>());
            services.AddSingleton<FakeMelhorEnvioShipmentClient>();
            services.AddSingleton<IMelhorEnvioShipmentClient>(sp => sp.GetRequiredService<FakeMelhorEnvioShipmentClient>());
            // J3: Fake em Testing — zero rede. Produção registra J3Client real abaixo.
            services.AddSingleton<FakeJ3Client>();
            services.AddSingleton<IJ3Client>(sp => sp.GetRequiredService<FakeJ3Client>());
            services.AddSingleton<FakeJ3FulfillmentClient>();
            services.AddSingleton<IJ3FulfillmentClient>(sp => sp.GetRequiredService<FakeJ3FulfillmentClient>());
            services.AddSingleton<FakeJ3ImportOrderByAccessKeyClient>();
            services.AddSingleton<IJ3ImportOrderByAccessKeyClient>(sp =>
                sp.GetRequiredService<FakeJ3ImportOrderByAccessKeyClient>());
            services.AddSingleton<FakeJ3SellerAuthProvider>();
            services.AddSingleton<IJ3SellerAuthProvider>(sp =>
                sp.GetRequiredService<FakeJ3SellerAuthProvider>());
            services.AddSingleton<FakeJ3OrderLookupClient>();
            services.AddSingleton<IJ3OrderLookupClient>(sp =>
                sp.GetRequiredService<FakeJ3OrderLookupClient>());
            services.AddSingleton<FakeJ3OrderDetailsClient>();
            services.AddSingleton<IJ3OrderDetailsClient>(sp =>
                sp.GetRequiredService<FakeJ3OrderDetailsClient>());
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

            services.AddHttpClient<IMelhorEnvioOAuthClient, MelhorEnvioOAuthHttpClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            services.AddHttpClient<IMelhorEnvioShipmentClient, MelhorEnvioShipmentHttpClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }

        // J3 GraphQL real somente fora de Testing (FakeJ3Client já registrado acima em testes).
        // Enabled=false: URL/token ausentes OK no startup; validação só ao invocar métodos (sem ValidateOnStart).
        // BaseAddress não é fixado: cada request usa J3_GRAPHQL_URL absoluto das options.
        if (!isTestEnvironment)
        {
            services.AddHttpClient(J3SellerAuthProvider.HttpClientName, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            });
            services.AddSingleton<IJ3SellerAuthProvider, J3SellerAuthProvider>();

            services.AddHttpClient<IJ3Client, J3Client>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            });

            // Mutation client: HttpClient separado (sem retry/Polly). Nenhum caller de produção neste passo.
            // AllowAutoRedirect=false: POST não deve seguir redirect (segunda request).
            services.AddHttpClient<IJ3FulfillmentClient, J3FulfillmentHttpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            });

            // importOrderByAccessKey — cliente separado; sem fallback para createTmsOrders.
            services.AddHttpClient<IJ3ImportOrderByAccessKeyClient, J3ImportOrderByAccessKeyHttpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            });

            // Lookup read-only searchOrderByCode — reconciliação admin.
            services.AddHttpClient<IJ3OrderLookupClient, J3OrderLookupHttpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false
            });

            // Lookup read-only getOrderDetails — hidratação de identificadores admin.
            services.AddHttpClient<IJ3OrderDetailsClient, J3OrderDetailsHttpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
                var seconds = opts.TimeoutSeconds > 0
                    ? Math.Clamp(opts.TimeoutSeconds, 3, 60)
                    : 15;
                client.Timeout = TimeSpan.FromSeconds(seconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false
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
        services.AddScoped<IUpSellerOrderExportService, UpSellerOrderExportService>();
        services.AddSingleton<IFiscalInvoiceXmlParser, FiscalInvoiceXmlParser>();
        services.AddScoped<IFiscalInvoiceImportService, FiscalInvoiceImportService>();
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
