using System.Data;
using Db2Simulator.Config;
using Microsoft.Data.SqlClient;

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
        using var context = OpenQueryTestContext.Create([TimestampMapping]);
        Skip.IfNot(context.SkipReason is null, context.SkipReason ?? "OPENQUERY unavailable");

        using var conn = context.OpenSqlServer();
        using var cmd = CreateOpenQueryCommand(conn, context.LinkedServerName!, TimestampMapping.Sql);
        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo);
        Assert.Equal(1, reader.FieldCount);
        Assert.Equal("TS", reader.GetName(0));
    }

    [SkippableFact]
    public void StandaloneDescribe_ReturnsTsColumnMetadata()
    {
        using var context = OpenQueryTestContext.Create([TimestampMapping]);
        Skip.IfNot(context.SkipReason is null, context.SkipReason ?? "OPENQUERY unavailable");

        using var conn = context.OpenSqlServer();
        using var cmd = CreateOpenQueryCommand(conn, context.LinkedServerName!, TimestampMapping.Sql);
        using var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
        var schema = reader.GetSchemaTable();
        Assert.NotNull(schema);
        Assert.Equal(1, schema!.Rows.Count);
        Assert.Equal("TS", schema.Rows[0]["ColumnName"]);
    }

    [SkippableFact]
    public void OpenQuery_ReturnsSingleRow()
    {
        using var context = OpenQueryTestContext.Create([TimestampMapping]);
        Skip.IfNot(context.SkipReason is null, context.SkipReason ?? "OPENQUERY unavailable");

        using var conn = context.OpenSqlServer();
        using var cmd = CreateOpenQueryCommand(conn, context.LinkedServerName!, TimestampMapping.Sql);
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.False(reader.IsDBNull(0));
        Assert.False(reader.Read());
    }

    private static SqlCommand CreateOpenQueryCommand(SqlConnection conn, string linkedServerName, string innerSql)
    {
        var cmd = conn.CreateCommand();
        string escapedSql = innerSql.Replace("'", "''");
        cmd.CommandText = $"SELECT * FROM OPENQUERY([{linkedServerName}], '{escapedSql}')";
        return cmd;
    }

    private sealed class OpenQueryTestContext : IDisposable
    {
        private readonly Db2TestSession? _simSession;
        private readonly SqlServerTestSession? _sqlSession;

        public string? SkipReason { get; }
        public string? LinkedServerName => _sqlSession?.LinkedServerName;

        private OpenQueryTestContext(Db2TestSession? simSession, SqlServerTestSession? sqlSession, string? skipReason)
        {
            _simSession = simSession;
            _sqlSession = sqlSession;
            SkipReason = skipReason;
        }

        public static OpenQueryTestContext Create(IReadOnlyList<MappingConfig> mappings)
        {
            var simSession = Db2TestSession.CreateEmbedded(mappings);
            if (simSession.SkipReason is not null)
                return Failed(simSession.SkipReason, simSession);

            if (simSession.SimulatorPort is null)
                return Failed("embedded simulator port unavailable", simSession);

            try
            {
                SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());
                DatabaseConnectionConfig? db2 = baseline.Tests.Db2;
                if (db2 is null || !db2.IsConfigured)
                    return Failed("tests.db2 is not configured", simSession);

                var linkedDb2 = new DatabaseConnectionConfig
                {
                    Host = "127.0.0.1",
                    Port = simSession.SimulatorPort.Value,
                    Database = baseline.Server.Database,
                    User = db2.User,
                    Password = db2.Password,
                };

                var sqlSession = SqlServerTestSession.CreateWithLinkedServer(linkedDb2);
                if (sqlSession.SkipReason is not null)
                    return Failed(sqlSession.SkipReason, simSession, sqlSession);

                return new OpenQueryTestContext(simSession, sqlSession, null);
            }
            catch (Exception ex)
            {
                return Failed(ex.Message, simSession);
            }
        }

        public SqlConnection OpenSqlServer() => _sqlSession!.Open();

        public void Dispose()
        {
            _sqlSession?.Dispose();
            _simSession?.Dispose();
        }

        private static OpenQueryTestContext Failed(string reason, Db2TestSession simSession, SqlServerTestSession? sqlSession = null)
        {
            sqlSession?.Dispose();
            simSession.Dispose();
            return new OpenQueryTestContext(null, null, reason);
        }
    }
}
