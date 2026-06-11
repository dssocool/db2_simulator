using Db2Simulator.Config;
using Microsoft.Data.SqlClient;

namespace Db2Simulator.Tests;

/// <summary>
/// Opens a SQL Server connection for integration tests. Reads tests.sqlServer from
/// config.json; tests are skipped when that section is omitted or incomplete.
/// When created via <see cref="CreateWithLinkedServer"/>, provisions a temporary
/// DB2 linked server from tests.db2 and drops it on dispose.
/// </summary>
internal sealed class SqlServerTestSession : IDisposable
{
    private readonly string _connectionString;
    private readonly string? _linkedServerName;

    public string? SkipReason { get; }
    public SqlServerConnectionConfig? Config { get; }
    public string? LinkedServerName => _linkedServerName;

    private SqlServerTestSession(
        SqlServerConnectionConfig? config,
        string connectionString,
        string? linkedServerName,
        string? skipReason)
    {
        Config = config;
        _connectionString = connectionString;
        _linkedServerName = linkedServerName;
        SkipReason = skipReason;
    }

    public static SqlServerTestSession Create()
    {
        try
        {
            SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());
            SqlServerConnectionConfig? sqlServer = baseline.Tests.SqlServer;
            if (sqlServer is null || !sqlServer.IsConfigured)
                return new SqlServerTestSession(null, "", null, "tests.sqlServer is not configured");

            string connectionString = SqlServerTestConnection.BuildConnectionString(sqlServer);
            string? skipReason = SqlServerTestConnection.Probe(connectionString);
            return new SqlServerTestSession(sqlServer, connectionString, null, skipReason);
        }
        catch (Exception ex)
        {
            return new SqlServerTestSession(null, "", null, ex.Message);
        }
    }

    public static SqlServerTestSession CreateWithLinkedServer(DatabaseConnectionConfig db2Target)
    {
        try
        {
            SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());
            SqlServerConnectionConfig? sqlServer = baseline.Tests.SqlServer;
            if (sqlServer is null || !sqlServer.IsConfigured)
                return new SqlServerTestSession(null, "", null, "tests.sqlServer is not configured");

            if (!db2Target.IsConfigured)
                return new SqlServerTestSession(null, "", null, "tests.db2 is not configured");

            string connectionString = SqlServerTestConnection.BuildConnectionString(sqlServer);
            string? skipReason = SqlServerTestConnection.Probe(connectionString);
            if (skipReason is not null)
                return new SqlServerTestSession(sqlServer, connectionString, null, skipReason);

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            skipReason = SqlServerLinkedServerManager.CheckProvider(conn);
            if (skipReason is not null)
                return new SqlServerTestSession(sqlServer, connectionString, null, skipReason);

            string linkedServerName = SqlServerLinkedServerManager.GenerateServerName();
            try
            {
                SqlServerLinkedServerManager.Create(conn, linkedServerName, db2Target);
            }
            catch (Exception ex)
            {
                return new SqlServerTestSession(sqlServer, connectionString, null, $"Failed to create linked server: {ex.Message}");
            }

            return new SqlServerTestSession(sqlServer, connectionString, linkedServerName, null);
        }
        catch (Exception ex)
        {
            return new SqlServerTestSession(null, "", null, ex.Message);
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
        if (_linkedServerName is null)
            return;

        try
        {
            using var conn = Open();
            SqlServerLinkedServerManager.Drop(conn, _linkedServerName);
        }
        catch
        {
            // Best-effort cleanup so test failures still surface.
        }
    }
}
