using Esotera.Api;
using Esotera.Application.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

public class DatabaseAutoMigrateTests
{
    [Fact]
    public void Absent_IsDisabled()
    {
        DatabaseAutoMigrate.IsExplicitlyEnabled(null).Should().BeFalse();
        DatabaseAutoMigrate.IsExplicitlyEnabled("").Should().BeFalse();
        DatabaseAutoMigrate.IsExplicitlyEnabled("   ").Should().BeFalse();
        ShouldApply("Production", null).Should().BeFalse();
        ShouldApply("Development", null).Should().BeFalse();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("1")]
    public void FalseOrNonTrue_IsDisabled(string raw)
    {
        DatabaseAutoMigrate.IsExplicitlyEnabled(raw).Should().BeFalse();
        ShouldApply("Production", raw).Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData(" true ")]
    public void True_EnablesForNonTesting(string raw)
    {
        DatabaseAutoMigrate.IsExplicitlyEnabled(raw).Should().BeTrue();
        ShouldApply("Production", raw).Should().BeTrue();
        ShouldApply("Development", raw).Should().BeTrue();
        ShouldApply("Staging", raw).Should().BeTrue();
    }

    [Fact]
    public void Testing_NeverEnables_EvenIfTrue()
    {
        ShouldApply("Testing", "true").Should().BeFalse();
        ShouldApply("Testing", "false").Should().BeFalse();
        ShouldApply("Testing", null).Should().BeFalse();
    }

    [Fact]
    public void TestingHost_StartsWithoutAutoMigrate_J3FlagsUnchanged()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        DatabaseAutoMigrate.ShouldApplyAtStartup("Testing", config).Should().BeFalse();

        var j3 = scope.ServiceProvider.GetRequiredService<IOptions<J3ShippingOptions>>().Value;
        j3.Enabled.Should().BeTrue("factory de teste liga quote J3 fake; produção permanece false");
        j3.FulfillmentEnabled.Should().BeFalse();
    }

    [Fact]
    public void J3ShippingOptions_ClassDefaults_RemainFalse()
    {
        var opts = new J3ShippingOptions();
        opts.Enabled.Should().BeFalse();
        opts.FulfillmentEnabled.Should().BeFalse();
    }

    private static bool ShouldApply(string environment, string? dbAutoMigrate)
    {
        var values = new Dictionary<string, string?>();
        if (dbAutoMigrate is not null)
            values[DatabaseAutoMigrate.ConfigurationKey] = dbAutoMigrate;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return DatabaseAutoMigrate.ShouldApplyAtStartup(environment, config);
    }
}
