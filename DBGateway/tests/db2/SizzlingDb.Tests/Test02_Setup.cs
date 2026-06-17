using SizzlingDb.Config;
using IBM.Data.Db2;
using Microsoft.Data.SqlClient;

namespace SizzlingDb.Tests;

/// <summary>
/// Step 2: provision everything the comparison tests need — a linked server in
/// SQL Server pointing at the real DB2, a test table with data in DB2, and a
/// mirror database/table with the same data in SQL Server. Setup is idempotent
/// (drop/recreate) and the objects are left in place for Test03 and debugging.
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test02_Setup
{
    [Fact]
    public void T01_CreateLinkedServer_ToRealDb2()
    {
        var sqlServer = TestConfig.RequireSqlServer();
        var db2 = TestConfig.RequireDb2();

        using var conn = SqlServerTestConnection.Open(sqlServer);
        SqlServerLinkedServerManager.Recreate(conn, TestObjects.LinkedServerName, db2);

        Assert.True(
            SqlServerLinkedServerManager.Exists(conn, TestObjects.LinkedServerName),
            $"linked server {TestObjects.LinkedServerName} was not created");

        SqlServerLinkedServerManager.TestLink(conn, TestObjects.LinkedServerName);
    }

    [Fact]
    public void T02_CreateDb2TestTable_WithData()
    {
        var db2 = TestConfig.RequireDb2();

        using var conn = Db2TestConnection.Open(db2);
        DropDb2TableIfExists(conn);
        Execute(conn, $"""
            CREATE TABLE {TestObjects.TableName} (
                ID INTEGER NOT NULL PRIMARY KEY,
                NAME VARCHAR(50),
                CODE CHAR(5),
                SMALL_VAL SMALLINT,
                BIG_VAL BIGINT,
                PRICE DECIMAL(9,2),
                RATIO DOUBLE,
                BORN DATE,
                WAKES TIME,
                CREATED TIMESTAMP
            )
            """);

        foreach (SampleRow row in TestObjects.Rows)
            InsertDb2Row(conn, row);

        Assert.Equal(TestObjects.Rows.Count, CountRows(conn, $"SELECT COUNT(*) FROM {TestObjects.TableName}"));
    }

    [Fact]
    public void T03_CreateSqlServerMirrorDatabase_WithSameData()
    {
        var sqlServer = TestConfig.RequireSqlServer();

        using (var master = SqlServerTestConnection.Open(sqlServer))
        {
            Execute(master, $"IF DB_ID(N'{TestObjects.SqlServerDatabase}') IS NULL CREATE DATABASE [{TestObjects.SqlServerDatabase}];");
        }

        using var conn = SqlServerTestConnection.Open(sqlServer, TestObjects.SqlServerDatabase);
        Execute(conn, $"IF OBJECT_ID(N'dbo.{TestObjects.TableName}') IS NOT NULL DROP TABLE dbo.{TestObjects.TableName};");
        Execute(conn, $"""
            CREATE TABLE dbo.{TestObjects.TableName} (
                ID INT NOT NULL PRIMARY KEY,
                NAME NVARCHAR(50) NULL,
                CODE NCHAR(5) NULL,
                SMALL_VAL SMALLINT NULL,
                BIG_VAL BIGINT NULL,
                PRICE DECIMAL(9,2) NULL,
                RATIO FLOAT NULL,
                BORN DATE NULL,
                WAKES TIME(0) NULL,
                CREATED DATETIME2(6) NULL
            );
            """);

        foreach (SampleRow row in TestObjects.Rows)
            InsertSqlServerRow(conn, row);

        Assert.Equal(TestObjects.Rows.Count, CountRows(conn, $"SELECT COUNT(*) FROM dbo.{TestObjects.TableName}"));
    }

    [Fact]
    public void T04_LinkedServer_AnswersDb2Query()
    {
        var sqlServer = TestConfig.RequireSqlServer();
        TestConfig.RequireDb2();

        using var conn = SqlServerTestConnection.Open(sqlServer);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT * FROM OPENQUERY([{TestObjects.LinkedServerName}], 'SELECT 1 AS ONE FROM SYSIBM.SYSDUMMY1')";

        Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
    }

    private static void DropDb2TableIfExists(DB2Connection conn)
    {
        try
        {
            Execute(conn, $"DROP TABLE {TestObjects.TableName}");
        }
        catch (DB2Exception ex) when (ex.Errors.Count > 0 && ex.Errors[0].NativeError == -204)
        {
            // Table did not exist yet.
        }
    }

    private static void InsertDb2Row(DB2Connection conn, SampleRow row)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {TestObjects.TableName} ({TestObjects.AllColumns}) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
        AddDb2Parameter(cmd, DB2Type.Integer, row.Id);
        AddDb2Parameter(cmd, DB2Type.VarChar, row.Name);
        AddDb2Parameter(cmd, DB2Type.Char, row.Code);
        AddDb2Parameter(cmd, DB2Type.SmallInt, row.SmallVal);
        AddDb2Parameter(cmd, DB2Type.BigInt, row.BigVal);
        AddDb2Parameter(cmd, DB2Type.Decimal, row.Price);
        AddDb2Parameter(cmd, DB2Type.Double, row.Ratio);
        AddDb2Parameter(cmd, DB2Type.Date, row.Born);
        AddDb2Parameter(cmd, DB2Type.Time, row.Wakes);
        AddDb2Parameter(cmd, DB2Type.Timestamp, row.Created);
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    private static void AddDb2Parameter(DB2Command cmd, DB2Type type, object? value)
    {
        var parameter = cmd.CreateParameter();
        parameter.DB2Type = type;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }

    private static void InsertSqlServerRow(SqlConnection conn, SampleRow row)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO dbo.{TestObjects.TableName} ({TestObjects.AllColumns})
            VALUES (@id, @name, @code, @smallVal, @bigVal, @price, @ratio, @born, @wakes, @created)
            """;
        cmd.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = row.Id });
        cmd.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 50) { Value = (object?)row.Name ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@code", System.Data.SqlDbType.NChar, 5) { Value = (object?)row.Code ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@smallVal", System.Data.SqlDbType.SmallInt) { Value = (object?)row.SmallVal ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@bigVal", System.Data.SqlDbType.BigInt) { Value = (object?)row.BigVal ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@price", System.Data.SqlDbType.Decimal) { Precision = 9, Scale = 2, Value = (object?)row.Price ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ratio", System.Data.SqlDbType.Float) { Value = (object?)row.Ratio ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@born", System.Data.SqlDbType.Date) { Value = (object?)row.Born ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@wakes", System.Data.SqlDbType.Time) { Value = (object?)row.Wakes ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@created", System.Data.SqlDbType.DateTime2) { Value = (object?)row.Created ?? DBNull.Value });
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    private static void Execute(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int CountRows(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
