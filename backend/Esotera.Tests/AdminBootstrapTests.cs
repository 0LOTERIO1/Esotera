using Esotera.Application.Interfaces;
using Esotera.Domain.Enums;
using Esotera.Infrastructure.Persistence;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esotera.Tests;

public class AdminBootstrapTests
{
    private static (EsoteraDbContext Db, AdminBootstrap Bootstrap) Create(
        Dictionary<string, string?> values)
    {
        var options = new DbContextOptionsBuilder<EsoteraDbContext>()
            .UseInMemoryDatabase($"bootstrap_{Guid.NewGuid():N}")
            .Options;
        var db = new EsoteraDbContext(options);
        IPasswordHasher hasher = new BcryptPasswordHasher();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var bootstrap = new AdminBootstrap(db, hasher, config, NullLogger<AdminBootstrap>.Instance);
        return (db, bootstrap);
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        var (db, bootstrap) = Create(new Dictionary<string, string?>
        {
            ["BOOTSTRAP_ADMIN_ENABLED"] = "false",
            ["BOOTSTRAP_ADMIN_NAME"] = "Admin",
            ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@example.com",
            ["BOOTSTRAP_ADMIN_PASSWORD"] = "senha123"
        });

        await bootstrap.RunAsync();

        db.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabled_CreatesAdminWithHashedPasswordAndRole()
    {
        var email = $"admin{Guid.NewGuid():N}@example.com";
        var password = "senhaSegura1";
        var (db, bootstrap) = Create(new Dictionary<string, string?>
        {
            ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
            ["BOOTSTRAP_ADMIN_NAME"] = "Admin Real",
            ["BOOTSTRAP_ADMIN_EMAIL"] = email,
            ["BOOTSTRAP_ADMIN_PASSWORD"] = password
        });

        await bootstrap.RunAsync();

        var user = await db.Users.SingleAsync();
        user.Email.Should().Be(email.ToLowerInvariant());
        user.Name.Should().Be("Admin Real");
        user.Role.Should().Be(UserRole.Admin);
        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBe(password);
        new BcryptPasswordHasher().Verify(password, user.PasswordHash).Should().BeTrue();
        user.Role.ToString().Should().Be("Admin");
    }

    [Fact]
    public async Task ExistingEmail_DoesNotRecreateOrChangePassword()
    {
        var email = $"admin{Guid.NewGuid():N}@example.com";
        var (db, bootstrap) = Create(new Dictionary<string, string?>
        {
            ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
            ["BOOTSTRAP_ADMIN_NAME"] = "Admin Real",
            ["BOOTSTRAP_ADMIN_EMAIL"] = email,
            ["BOOTSTRAP_ADMIN_PASSWORD"] = "senhaSegura1"
        });

        await bootstrap.RunAsync();
        var originalHash = (await db.Users.SingleAsync()).PasswordHash;

        var second = new AdminBootstrap(
            db,
            new BcryptPasswordHasher(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
                ["BOOTSTRAP_ADMIN_NAME"] = "Outro Nome",
                ["BOOTSTRAP_ADMIN_EMAIL"] = email,
                ["BOOTSTRAP_ADMIN_PASSWORD"] = "outraSenha99"
            }).Build(),
            NullLogger<AdminBootstrap>.Instance);

        await second.RunAsync();

        var users = await db.Users.ToListAsync();
        users.Should().HaveCount(1);
        users[0].Name.Should().Be("Admin Real");
        users[0].PasswordHash.Should().Be(originalHash);
    }

    [Fact]
    public async Task IncompleteEnv_DoesNotCreate()
    {
        var (db, bootstrap) = Create(new Dictionary<string, string?>
        {
            ["BOOTSTRAP_ADMIN_ENABLED"] = "true",
            ["BOOTSTRAP_ADMIN_NAME"] = "Admin",
            ["BOOTSTRAP_ADMIN_EMAIL"] = "admin@example.com"
            // password missing
        });

        await bootstrap.RunAsync();

        db.Users.Should().BeEmpty();
    }
}
