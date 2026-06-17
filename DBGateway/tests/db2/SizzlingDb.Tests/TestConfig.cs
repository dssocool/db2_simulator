using SizzlingDb.Config;

namespace SizzlingDb.Tests;

/// <summary>
/// Loads config/config.json once for the whole test run and hands out the
/// integration-test connection targets. Tests are skipped when config.json or
/// the relevant tests.* section is missing; everything else is a real failure.
/// </summary>
internal static class TestConfig
{
    private static readonly Lazy<(SizzlingDbConfig? Config, string? Error)> Loaded = new(Load);

    public static DatabaseConnectionConfig RequireDb2()
    {
        SizzlingDbConfig config = Require();
        DatabaseConnectionConfig? db2 = config.Tests.Db2;
        Skip.If(db2 is null || !db2.IsConfigured, "tests.db2 is not configured in config/config.json");
        return db2!;
    }

    public static SqlServerConnectionConfig RequireSqlServer()
    {
        SizzlingDbConfig config = Require();
        SqlServerConnectionConfig? sqlServer = config.Tests.SqlServer;
        Skip.If(sqlServer is null || !sqlServer.IsConfigured, "tests.sqlServer is not configured in config/config.json");
        return sqlServer!;
    }

    private static SizzlingDbConfig Require()
    {
        (SizzlingDbConfig? config, string? error) = Loaded.Value;
        Skip.If(config is null, $"config/config.json could not be loaded: {error}");
        return config!;
    }

    private static (SizzlingDbConfig?, string?) Load()
    {
        try
        {
            return (SizzlingDbConfig.Load(ConfigPath.Resolve()), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
