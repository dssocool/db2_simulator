using SizzlingDb.Backends.SqlServer.Sql;
using SizzlingDb.Config;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Step 3: compare SELECT results from the real SQL Server vs the SqlClient forwarder
/// (same path the gateway uses when SQL does not match data.json / default_data.json).
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test03_ForwarderComparison
{
    private static SqlServerForwarder Forwarder => new(CreateForwardConfig());

    [Fact]
    public void T01_Integer_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_INT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T02_Bit_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_BIT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T03_TinyInt_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_TINYINT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T04_SmallInt_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_SMALLINT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T05_BigInt_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_BIGINT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T06_Real_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_REAL FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T07_Float_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_FLOAT FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T08_Decimal_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_DECIMAL FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T09_Numeric_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_NUMERIC FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T10_Money_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_MONEY FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T11_SmallMoney_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_SMALLMONEY FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T12_Char_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_CHAR FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T13_Varchar_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_VARCHAR FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T14_NChar_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_NCHAR FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T15_NVarchar_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_NVARCHAR FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T16_Binary_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_BINARY FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T17_VarBinary_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_VARBINARY FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T18_Date_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_DATE FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T19_Time_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_TIME FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T20_DateTime_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_DATETIME FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T21_DateTime2_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_DATETIME2 FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T22_SmallDateTime_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_SMALLDATETIME FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T23_DateTimeOffset_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_DATETIMEOFFSET FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T24_UniqueIdentifier_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_UNIQUEIDENTIFIER FROM dbo.{TestObjects.TableName} WHERE ID = 1");

    [Fact]
    public void T25_NullInteger_MatchesDirect() =>
        AssertForwardMatchesDirect($"SELECT COL_INT FROM dbo.{TestObjects.TableName} WHERE ID = 3");

    private static void AssertForwardMatchesDirect(string sql)
    {
        var sqlServer = TestConfig.RequireSqlServer();
        ResultTable direct = QueryDirect(sqlServer, sql);
        ResultTable forwarded = QueryForwarder(sql);
        ResultTable.AssertEqual(direct, forwarded, "direct SQL Server", "SqlClient forwarder");
    }

    private static ResultTable QueryDirect(SqlServerConnectionConfig config, string sql)
    {
        using var conn = SqlServerTestConnection.Open(config, TestObjects.SqlServerDatabase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return ResultTable.Read(reader);
    }

    private static ResultTable QueryForwarder(string sql)
    {
        StatementResponse response = Forwarder.Execute(sql, TestObjects.SqlServerDatabase);
        var rs = Assert.IsType<ResultSetResponse>(response);
        return ResultTable.FromForwarder(rs);
    }

    private static SqlServerForwardConfig CreateForwardConfig()
    {
        var upstream = TestConfig.RequireSqlServer();
        return new SqlServerForwardConfig
        {
            Host = upstream.Host,
            Port = upstream.Port,
            Database = upstream.Database,
            User = upstream.User,
            Password = upstream.Password,
        };
    }
}
