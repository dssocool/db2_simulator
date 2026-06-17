namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Step 1: verify the real SQL Server from tests/config.json is reachable, and
/// that the TDS simulator accepts connections and answers mapped queries.
/// </summary>
[Collection(SimulatorCollection.Name)]
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test01_Connectivity
{
    private readonly SimulatorFixture _fixture;

    public Test01_Connectivity(SimulatorFixture fixture) => _fixture = fixture;

    [Fact]
    public void T01_RealSqlServer_IsReachable()
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

    [Fact]
    public void T02_Simulator_AcceptsLogin_AndAnswersCurrentTimestamp()
    {
        using var conn = SimulatorTestConnection.Open(_fixture);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CURRENT_TIMESTAMP AS TS";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("TS", reader.GetName(0));
        DateTime ts = reader.GetDateTime(0);
        Assert.Equal(2026, ts.Year);
        Assert.Equal(6, ts.Month);
        Assert.Equal(10, ts.Day);
        Assert.False(reader.Read());
    }

    [Fact]
    public void T03_Simulator_AcceptsGetDate_AliasMapping()
    {
        using var conn = SimulatorTestConnection.Open(_fixture);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT GETDATE() AS TS";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(2026, reader.GetDateTime(0).Year);
    }

    [Fact]
    public void T04_Simulator_RejectsBadPassword()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{_fixture.Port}",
            InitialCatalog = "master",
            UserID = "dev_user",
            Password = "wrong",
            TrustServerCertificate = true,
            Pooling = false,
        };
        builder["Encrypt"] = "False";

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        Assert.Throws<Microsoft.Data.SqlClient.SqlException>(() => conn.Open());
    }
}
