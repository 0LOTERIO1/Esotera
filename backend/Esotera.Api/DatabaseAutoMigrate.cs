using Microsoft.Extensions.Configuration;

namespace Esotera.Api;

/// <summary>
/// Decisão de MigrateAsync no startup. Default seguro: não migrar.
/// Testing nunca aplica (testes usam EnsureCreated).
/// </summary>
public static class DatabaseAutoMigrate
{
    public const string ConfigurationKey = "DB_AUTO_MIGRATE";

    public static bool ShouldApplyAtStartup(string? environmentName, IConfiguration configuration)
    {
        if (string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsExplicitlyEnabled(configuration[ConfigurationKey]);
    }

    /// <summary>Ausente, vazio ou qualquer valor que não seja true → desabilitado.</summary>
    public static bool IsExplicitlyEnabled(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return bool.TryParse(raw.Trim(), out var enabled) && enabled;
    }
}
