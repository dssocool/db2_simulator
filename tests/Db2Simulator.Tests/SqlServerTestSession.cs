using Db2Simulator.Config;
using Microsoft.Data.SqlClient;

namespace Db2Simulator.Tests;

/// <summary>
/// Opens a SQL Server connection for integration tests. Reads tests.sqlServer from
/// config.json; tests are skipped when that section is omitted or incomplete.
/// </summary>
internal sealed class SqlServerTestSession : IDisposable
{
    private readonly string _connectionString;

    public string? SkipReason { get; }
    public SqlServerConnectionConfig? Config { get; }

    private SqlServerTestSession(SqlServerConnectionConfig? config, string connectionString, string? skipReason)
    {
        Config = config;
        _connectionString = connectionString;
        SkipReason = skipReason;
    }

    public static SqlServerTestSession Create()
    {
        try
        {
            SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());
            SqlServerConnectionConfig? sqlServer = baseline.Tests.SqlServer;
            if (sqlServer is null || !sqlServer.IsConfigured)
                return new SqlServerTestSession(null, "", "tests.sqlServer is not configured");

            string connectionString = SqlServerTestConnection.BuildConnectionString(sqlServer);
            string? skipReason = SqlServerTestConnection.Probe(connectionString);
            return new SqlServerTestSession(sqlServer, connectionString, skipReason);
        }
        catch (Exception ex)
        {
            return new SqlServerTestSession(null, "", ex.Message);
        }
    }

    public SqlConnection Open()
    {
        if (SkipReason is not null)
            throw new InvalidOperationException(SkipReason);

        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Dispose()
    {
    }
}
