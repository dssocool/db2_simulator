using SizzlingDb.Config;

namespace SizzlingDb.Tests;

/// <summary>Loads tests/config.json for integration tests.</summary>
internal static class TestConfig
{
    private static readonly Lazy<IntegrationTestConfig> Loaded = new(Load);

    public static void EnsureLoaded() => _ = Loaded.Value;

    public static DatabaseConnectionConfig RequireDb2()
    {
        IntegrationTestConfig config = Loaded.Value;
        if (config.Db2 is null || !config.Db2.IsConfigured)
            throw new InvalidOperationException("db2 is not configured in tests/config.json");
        return config.Db2;
    }

    public static SqlServerConnectionConfig RequireSqlServer()
    {
        IntegrationTestConfig config = Loaded.Value;
        if (config.SqlServer is null || !config.SqlServer.IsConfigured)
            throw new InvalidOperationException("sqlServer is not configured in tests/config.json");
        return config.SqlServer;
    }

    private static IntegrationTestConfig Load() =>
        IntegrationTestConfig.Load(TestConfigPath.Resolve());
}
