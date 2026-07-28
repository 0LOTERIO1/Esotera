using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Esotera.Api.Middleware;
using Esotera.Api.Services;
using Esotera.Application;
using Esotera.Application.Interfaces;
using Esotera.Infrastructure;
using Esotera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var isTesting = builder.Environment.EnvironmentName == "Testing" ||
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";

if (!isTesting)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());
}

try
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    var jwtKey = builder.Configuration["Jwt:Key"] 
        ?? throw new InvalidOperationException("JWT Key not configured");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Esotera";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "EsoteraApp";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.NameIdentifier
            };
            options.MapInboundClaims = true;
        });

    builder.Services.AddAuthorization();

    var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:3000";
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            // Em testes, muitos registros em sequência — limite alto para não mascarar falhas reais
            limiter.PermitLimit = isTesting ? 10_000 : 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.QueueLimit = 0;
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Esotera API",
            Version = "v1",
            Description = "API para a loja esotérica Esotera"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    var isTestEnvironment = app.Environment.EnvironmentName == "Testing";

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Esotera API v1");
        });
    }

    if (!isTestEnvironment)
    {
        var storagePath = builder.Configuration["IMAGE_STORAGE_PATH"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "storage", "products");
        
        Directory.CreateDirectory(storagePath);
        
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.GetFullPath(storagePath)),
            RequestPath = "/media/products"
        });
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    var seedDevData = builder.Configuration.GetValue<bool>("SEED_DEV_DATA") 
        || app.Environment.IsDevelopment();

    if (!isTestEnvironment)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EsoteraDbContext>();
        
        await context.Database.MigrateAsync();
        
        if (seedDevData)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DevSeed>();
            await seeder.SeedAsync();
        }

        Log.Information("Esotera API iniciada em {Urls}", string.Join(", ", app.Urls));
    }

    app.Run();
}
catch (Exception ex) when (!isTesting)
{
    Log.Fatal(ex, "Aplicação encerrada inesperadamente");
    throw;
}
finally
{
    if (!isTesting)
    {
        Log.CloseAndFlush();
    }
}

public partial class Program { }
