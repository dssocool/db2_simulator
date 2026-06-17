namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Step 4: gateway TDS integration — mapped queries through the simulator.
/// </summary>
[Collection(SimulatorCollection.Name)]
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test04_GatewayIntegration
{
    private readonly SimulatorFixture _simulator;

    public Test04_GatewayIntegration(SimulatorFixture simulator) => _simulator = simulator;

    [Fact]
    public void T01_MappedQuery_ReturnsConfiguredResult()
    {
        using var conn = SimulatorTestConnection.Open(_simulator);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CURRENT_TIMESTAMP AS TS";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(2026, reader.GetDateTime(0).Year);
        Assert.False(reader.Read());
    }
}

/// <summary>
/// Forwarded SELECT through the gateway (requires correct TDS result re-encoding).
/// </summary>
[Collection(GatewayCollection.Name)]
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test04_GatewayForwarding
{
    private readonly GatewayFixture _gateway;

    public Test04_GatewayForwarding(GatewayFixture gateway) => _gateway = gateway;

    [Fact(Skip = "Gateway TDS re-encoding for forwarded results is not yet SqlClient-compatible")]
    public void T01_ForwardedSingleColumn_MatchesDirectSqlServer()
    {
        const string sql = "SELECT COL_INT FROM dbo.SIZZLINGDB_TYPES WHERE ID = 1";
        var sqlServer = TestConfig.RequireSqlServer();

        ResultTable direct = QueryDirect(sqlServer, sql);
        ResultTable gateway = QueryGateway(sql);

        ResultTable.AssertEqual(direct, gateway, "direct SQL Server", "gateway");
    }

    private static ResultTable QueryDirect(SizzlingDb.Config.SqlServerConnectionConfig config, string sql)
    {
        using var conn = SqlServerTestConnection.Open(config, TestObjects.SqlServerDatabase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return ResultTable.Read(reader);
    }

    private ResultTable QueryGateway(string sql)
    {
        using var conn = GatewayTestConnection.Open(_gateway);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return ResultTable.Read(reader);
    }
}
