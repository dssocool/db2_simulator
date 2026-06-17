using SizzlingDb.Config;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Loads tests/sqlserver/config.json for integration tests against a real SQL Server.
/// </summary>
internal static class TestConfig
{
    private static readonly Lazy<(IntegrationTestConfig? Config, string? Error)> Loaded = new(Load);

    public static SqlServerConnectionConfig RequireSqlServer()
    {
        IntegrationTestConfig config = Require();
        SqlServerConnectionConfig? sqlServer = config.SqlServer;
        Skip.If(sqlServer is null || !sqlServer.IsConfigured, "sqlServer is not configured in tests/sqlserver/config.json");
        return sqlServer!;
    }

    private static IntegrationTestConfig Require()
    {
        (IntegrationTestConfig? config, string? error) = Loaded.Value;
        Skip.If(config is null, $"tests/sqlserver/config.json could not be loaded: {error}");
        return config!;
    }

    private static (IntegrationTestConfig?, string?) Load()
    {
        try
        {
            return (IntegrationTestConfig.Load(TestConfigPath.Resolve()), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
