using SizzlingDb.Config;

namespace SizzlingDb.Tests;

/// <summary>
/// Step 3: run the same DB2 SQL through the SQL Server linked server (OPENQUERY)
/// and directly against the real DB2, then compare the two result sets cell by
/// cell. Requires the objects provisioned by Test02_Setup.
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test03_QueryComparison
{
    [SkippableFact]
    public void T01_AllColumnsAllRows_MatchDirectDb2() =>
        AssertLinkedMatchesDirect(
            $"SELECT {TestObjects.AllColumns} FROM {TestObjects.TableName} ORDER BY ID");

    [SkippableFact]
    public void T02_FilterByKey_MatchesDirectDb2() =>
        AssertLinkedMatchesDirect(
            $"SELECT {TestObjects.AllColumns} FROM {TestObjects.TableName} WHERE ID = 2");

    [SkippableFact]
    public void T03_RowWithAllNulls_MatchesDirectDb2() =>
        AssertLinkedMatchesDirect(
            $"SELECT {TestObjects.AllColumns} FROM {TestObjects.TableName} WHERE ID = 4");

    [SkippableFact]
    public void T04_Aggregates_MatchDirectDb2() =>
        AssertLinkedMatchesDirect($"""
            SELECT COUNT(*) AS CNT,
                   SUM(SMALL_VAL) AS SUM_SMALL,
                   MIN(PRICE) AS MIN_PRICE,
                   MAX(BORN) AS MAX_BORN
            FROM {TestObjects.TableName}
            """);

    [SkippableFact]
    public void T05_StringFunctions_MatchDirectDb2() =>
        AssertLinkedMatchesDirect(
            $"SELECT UPPER(NAME) AS UNAME, LENGTH(NAME) AS NAME_LEN FROM {TestObjects.TableName} WHERE ID = 1");

    [SkippableFact]
    public void T06_ScalarExpressions_MatchDirectDb2() =>
        AssertLinkedMatchesDirect("""
            SELECT CAST(42 AS INTEGER) AS N,
                   CAST('ABC' AS VARCHAR(10)) AS S,
                   CAST('1.50' AS DECIMAL(5,2)) AS D
            FROM SYSIBM.SYSDUMMY1
            """);

    [SkippableFact]
    public void T07_LinkedServerData_MatchesSqlServerMirrorTable()
    {
        var sqlServer = TestConfig.RequireSqlServer();
        TestConfig.RequireDb2();

        string select = $"SELECT {TestObjects.AllColumns} FROM {TestObjects.TableName} ORDER BY ID";

        ResultTable mirror = QuerySqlServer(sqlServer, $"SELECT {TestObjects.AllColumns} FROM dbo.{TestObjects.TableName} ORDER BY ID", TestObjects.SqlServerDatabase);
        ResultTable linked = QueryThroughLinkedServer(sqlServer, select);

        ResultTable.AssertEqual(mirror, linked, "SQL Server mirror", "linked server");
    }

    /// <summary>The core comparison: same DB2 SQL via OPENQUERY and via the IBM driver.</summary>
    private static void AssertLinkedMatchesDirect(string db2Sql)
    {
        var db2 = TestConfig.RequireDb2();
        var sqlServer = TestConfig.RequireSqlServer();

        ResultTable direct = QueryDb2Direct(db2, db2Sql);
        ResultTable linked = QueryThroughLinkedServer(sqlServer, db2Sql);

        ResultTable.AssertEqual(direct, linked, "direct DB2", "linked server");
    }

    private static ResultTable QueryDb2Direct(DatabaseConnectionConfig config, string sql)
    {
        using var conn = Db2TestConnection.Open(config);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return ResultTable.Read(reader);
    }

    private static ResultTable QueryThroughLinkedServer(SqlServerConnectionConfig config, string db2Sql)
    {
        string escaped = db2Sql.Replace("'", "''");
        return QuerySqlServer(config, $"SELECT * FROM OPENQUERY([{TestObjects.LinkedServerName}], '{escaped}')");
    }

    private static ResultTable QuerySqlServer(SqlServerConnectionConfig config, string sql, string? database = null)
    {
        using var conn = SqlServerTestConnection.Open(config, database);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        return ResultTable.Read(reader);
    }
}
