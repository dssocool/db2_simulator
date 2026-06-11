namespace Db2Simulator.Tests;

/// <summary>
/// Integration tests against tests.sqlServer in config.json. These fail (not skip)
/// when the section is missing or the server is unreachable.
/// </summary>
public sealed class SqlServerConnectivityTests
{
    [Fact]
    public void CanConnectAndQueryMaster()
    {
        using var session = SqlServerTestSession.Create();
        Assert.Null(session.SkipReason);

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DB_NAME()";
        var dbName = cmd.ExecuteScalar() as string;

        Assert.Equal(session.Config!.Database, dbName);
    }

    [Fact]
    public void CanReadServerName()
    {
        using var session = SqlServerTestSession.Create();
        Assert.Null(session.SkipReason);

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT @@SERVERNAME";
        var serverName = cmd.ExecuteScalar() as string;

        Assert.False(string.IsNullOrWhiteSpace(serverName));
    }
}
