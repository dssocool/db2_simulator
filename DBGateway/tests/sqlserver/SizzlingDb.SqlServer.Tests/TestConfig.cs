using SizzlingDb.Config;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>Loads tests/config.json; required for every test run.</summary>
internal static class TestConfig
{
    private static readonly Lazy<IntegrationTestConfig> Loaded = new(Load);

    public static void EnsureLoaded() => _ = Loaded.Value;

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
