using System.Data;
using Db2Simulator.Config;
using IBM.Data.Db2;

namespace Db2Simulator.Tests;

/// <summary>
/// High-level coverage of the DB2OLEDB OPENQUERY flow: prepare-with-describe,
/// standalone describe, and open-query execution.
/// </summary>
public sealed class OpenQueryTests
{
    private static readonly MappingConfig TimestampMapping = new()
    {
        Sql = "SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1",
        Match = MatchKind.Exact,
        Result = TestResultRows.Result(
            [new ColumnConfig { Name = "TS", Type = "TIMESTAMP", Nullable = true }],
            new object?[] { "2026-06-10-14.30.00.000000" }),
    };

    [SkippableFact]
    public void PrepareWithDescribe_ReturnsTsColumnMetadata()
    {
        using var session = Db2TestSession.Create([TimestampMapping]);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = TimestampMapping.Sql;

        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo);
        Assert.Equal(1, reader.FieldCount);
        Assert.Equal("TS", reader.GetName(0));
    }

    [SkippableFact]
    public void StandaloneDescribe_ReturnsTsColumnMetadata()
    {
        using var session = Db2TestSession.Create([TimestampMapping]);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = TimestampMapping.Sql;

        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
        var schema = reader.GetSchemaTable();
        Assert.NotNull(schema);
        Assert.Equal(1, schema!.Rows.Count);
        Assert.Equal("TS", schema.Rows[0]["ColumnName"]);
    }

    [SkippableFact]
    public void OpenQuery_ReturnsSingleRow()
    {
        using var session = Db2TestSession.Create([TimestampMapping]);
        Skip.IfNot(session.SkipReason is null, session.SkipReason ?? "DB2 unavailable");

        using var conn = session.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = TimestampMapping.Sql;

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.False(reader.IsDBNull(0));
        Assert.False(reader.Read());
    }
}
