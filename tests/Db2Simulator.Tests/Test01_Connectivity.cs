namespace Db2Simulator.Tests;

/// <summary>
/// Step 1: verify the real SQL Server (tests.sqlServer) and the real DB2
/// (tests.db2) from config/config.json are reachable and answer simple queries.
/// Everything later in the suite depends on these two connections.
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test01_Connectivity
{
    [SkippableFact]
    public void T01_SqlServer_IsReachable_AndAnswersQueries()
    {
        var config = TestConfig.RequireSqlServer();

        using var conn = SqlServerTestConnection.Open(config);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DB_NAME(), @@SERVERNAME";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(config.Database, reader.GetString(0), ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(1)));
    }

    [SkippableFact]
    public void T02_Db2_IsReachable_AndAnswersQueries()
    {
        var config = TestConfig.RequireDb2();

        using var conn = Db2TestConnection.Open(config);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM SYSIBM.SYSDUMMY1";

        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
    }
}
