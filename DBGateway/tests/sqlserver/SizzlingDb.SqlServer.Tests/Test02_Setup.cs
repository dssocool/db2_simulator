using Microsoft.Data.SqlClient;
using SizzlingDb.Config;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Step 2: provision the test database and comprehensive type table on the real
/// SQL Server. Setup is idempotent (drop/recreate) and left in place for later tests.
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test02_Setup
{
    [Fact]
    public void T01_CreateTestDatabase_AndTypesTable()
    {
        var sqlServer = TestConfig.RequireSqlServer();

        using (var master = SqlServerTestConnection.Open(sqlServer))
        {
            Execute(master, $"""
                IF DB_ID(N'{TestObjects.SqlServerDatabase}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{TestObjects.SqlServerDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{TestObjects.SqlServerDatabase}];
                END
                CREATE DATABASE [{TestObjects.SqlServerDatabase}];
                """);
        }

        using var conn = SqlServerTestConnection.Open(sqlServer, TestObjects.SqlServerDatabase);
        Execute(conn, $"IF OBJECT_ID(N'dbo.{TestObjects.TableName}') IS NOT NULL DROP TABLE dbo.{TestObjects.TableName};");
        Execute(conn, $"""
            CREATE TABLE dbo.{TestObjects.TableName} (
                ID INT NOT NULL PRIMARY KEY,
                COL_BIT BIT NULL,
                COL_TINYINT TINYINT NULL,
                COL_SMALLINT SMALLINT NULL,
                COL_INT INT NULL,
                COL_BIGINT BIGINT NULL,
                COL_REAL REAL NULL,
                COL_FLOAT FLOAT NULL,
                COL_DECIMAL DECIMAL(18,4) NULL,
                COL_NUMERIC NUMERIC(10,2) NULL,
                COL_MONEY MONEY NULL,
                COL_SMALLMONEY SMALLMONEY NULL,
                COL_CHAR CHAR(5) NULL,
                COL_VARCHAR VARCHAR(50) NULL,
                COL_NCHAR NCHAR(5) NULL,
                COL_NVARCHAR NVARCHAR(50) NULL,
                COL_BINARY BINARY(4) NULL,
                COL_VARBINARY VARBINARY(16) NULL,
                COL_DATE DATE NULL,
                COL_TIME TIME(3) NULL,
                COL_DATETIME DATETIME NULL,
                COL_DATETIME2 DATETIME2(6) NULL,
                COL_SMALLDATETIME SMALLDATETIME NULL,
                COL_DATETIMEOFFSET DATETIMEOFFSET(6) NULL,
                COL_UNIQUEIDENTIFIER UNIQUEIDENTIFIER NULL
            );
            """);

        foreach (TypeSampleRow row in TestObjects.Rows)
            InsertRow(conn, row);

        Assert.Equal(TestObjects.Rows.Count, CountRows(conn, $"SELECT COUNT(*) FROM dbo.{TestObjects.TableName}"));
    }

    private static void InsertRow(SqlConnection conn, TypeSampleRow row)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO dbo.{TestObjects.TableName} ({TestObjects.AllColumns})
            VALUES (
                @id, @bit, @tinyint, @smallint, @int, @bigint,
                @real, @float, @decimal, @numeric, @money, @smallmoney,
                @char, @varchar, @nchar, @nvarchar,
                @binary, @varbinary,
                @date, @time, @datetime, @datetime2, @smalldatetime, @datetimeoffset,
                @guid
            )
            """;
        cmd.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = row.Id });
        cmd.Parameters.Add(new SqlParameter("@bit", System.Data.SqlDbType.Bit) { Value = (object?)row.ColBit ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@tinyint", System.Data.SqlDbType.TinyInt) { Value = (object?)row.ColTinyint ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@smallint", System.Data.SqlDbType.SmallInt) { Value = (object?)row.ColSmallint ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@int", System.Data.SqlDbType.Int) { Value = (object?)row.ColInt ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@bigint", System.Data.SqlDbType.BigInt) { Value = (object?)row.ColBigint ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@real", System.Data.SqlDbType.Real) { Value = (object?)row.ColReal ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@float", System.Data.SqlDbType.Float) { Value = (object?)row.ColFloat ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@decimal", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 4, Value = (object?)row.ColDecimal ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@numeric", System.Data.SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = (object?)row.ColNumeric ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@money", System.Data.SqlDbType.Money) { Value = (object?)row.ColMoney ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@smallmoney", System.Data.SqlDbType.SmallMoney) { Value = (object?)row.ColSmallmoney ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@char", System.Data.SqlDbType.Char, 5) { Value = (object?)row.ColChar ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@varchar", System.Data.SqlDbType.VarChar, 50) { Value = (object?)row.ColVarchar ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@nchar", System.Data.SqlDbType.NChar, 5) { Value = (object?)row.ColNchar ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@nvarchar", System.Data.SqlDbType.NVarChar, 50) { Value = (object?)row.ColNvarchar ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@binary", System.Data.SqlDbType.Binary, 4) { Value = (object?)row.ColBinary ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@varbinary", System.Data.SqlDbType.VarBinary, 16) { Value = (object?)row.ColVarbinary ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@date", System.Data.SqlDbType.Date) { Value = (object?)row.ColDate ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@time", System.Data.SqlDbType.Time) { Scale = 3, Value = (object?)row.ColTime ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@datetime", System.Data.SqlDbType.DateTime) { Value = (object?)row.ColDatetime ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@datetime2", System.Data.SqlDbType.DateTime2) { Scale = 6, Value = (object?)row.ColDatetime2 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@smalldatetime", System.Data.SqlDbType.SmallDateTime) { Value = (object?)row.ColSmalldatetime ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@datetimeoffset", System.Data.SqlDbType.DateTimeOffset) { Scale = 6, Value = (object?)row.ColDatetimeoffset ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@guid", System.Data.SqlDbType.UniqueIdentifier) { Value = (object?)row.ColUniqueidentifier ?? DBNull.Value });
        Assert.Equal(1, cmd.ExecuteNonQuery());
    }

    private static void Execute(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int CountRows(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
