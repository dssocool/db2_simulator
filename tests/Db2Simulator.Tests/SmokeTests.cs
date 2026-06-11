using Db2Simulator.Config;
using IBM.Data.Db2;

namespace Db2Simulator.Tests;

public sealed class SmokeTests
{
    private static readonly MappingConfig TimestampMapping = new()
    {
        Sql = "SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1",
        Match = MatchKind.Exact,
        Result = TestResultRows.Result(
            [new ColumnConfig { Name = "TS", Type = "TIMESTAMP", Nullable = true }],
            new object?[] { "2026-06-10-14.30.00.000000" }),
    };

    private static readonly MappingConfig SimpleSelectMapping = new()
    {
        Sql = "SELECT 1 AS ONE FROM SYSIBM.SYSDUMMY1",
        Match = MatchKind.Exact,
        Result = TestResultRows.Result(
            [new ColumnConfig { Name = "ONE", Type = "INTEGER", Nullable = false }],
            new object?[] { 1 }),
    };

    private static readonly DefaultResponseConfig UnmappedErrorResponse = new()
    {
        Error = new ErrorConfig
        {
            Sqlcode = -204,
            Sqlstate = "42704",
            Message = "DB2SIM: no mapping configured for the requested statement",
        },
    };

    [SkippableFact]
    public void Timestamp_ReturnsSingleRowWithTsColumn()
    {
        using var session = Db2TestSession.CreateEmbedded([TimestampMapping]);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = TimestampMapping.Sql;

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("TS", reader.GetName(0));
        Assert.False(reader.IsDBNull(0));
        Assert.False(reader.Read());
    }

    [SkippableFact]
    public void SimpleSelect_ReturnsSingleRow()
    {
        using var session = Db2TestSession.CreateEmbedded([SimpleSelectMapping]);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SimpleSelectMapping.Sql;

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.False(reader.Read());
    }

    [SkippableFact]
    public void BadPassword_IsRejected()
    {
        using var session = Db2TestSession.CreateEmbedded();
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        var ex = Assert.Throws<DB2Exception>(() =>
        {
            using var conn = session.Open("wrong");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM SYSIBM.SYSDUMMY1";
            cmd.ExecuteScalar();
        });

        Assert.NotEqual(0, ex.ErrorCode);
    }

    [SkippableFact]
    public void UnmappedQuery_ReturnsSqlCode204()
    {
        using var session = Db2TestSession.CreateEmbedded(defaultResponse: UnmappedErrorResponse);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM NOSUCHTABLE";

        var ex = Assert.Throws<DB2Exception>(() => cmd.ExecuteReader());
        Assert.NotEmpty(ex.Errors);
        Assert.Equal(-204, ex.Errors[0].NativeError);
        Assert.Equal("42704", ex.Errors[0].SQLState);
    }
}
