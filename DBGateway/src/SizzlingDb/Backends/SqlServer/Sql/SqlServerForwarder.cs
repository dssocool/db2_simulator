using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using SizzlingDb.Config;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.SqlServer.Sql;

/// <summary>Executes unmapped SQL on an upstream SQL Server via SqlClient.</summary>
internal sealed class SqlServerForwarder
{
    private readonly SqlServerForwardConfig _config;

    public SqlServerForwarder(SqlServerForwardConfig config) => _config = config;

    public StatementResponse Execute(string sql, string clientDatabase)
    {
        try
        {
            using var conn = Open(clientDatabase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            if (LooksLikeQuery(sql))
            {
                using var reader = cmd.ExecuteReader();
                return ReadResultSet(reader);
            }

            long count = cmd.ExecuteNonQuery();
            return new UpdateCountResponse(count);
        }
        catch (SqlException ex)
        {
            SqlError err = ex.Errors[0];
            return new ErrorResponse(err.Number, err.State.ToString("D2", CultureInfo.InvariantCulture), err.Message);
        }
    }

    private SqlConnection Open(string clientDatabase)
    {
        string database = string.IsNullOrWhiteSpace(clientDatabase) ? _config.Database : clientDatabase;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _config.DataSource,
            InitialCatalog = database,
            UserID = _config.User,
            Password = _config.Password,
            TrustServerCertificate = true,
            Encrypt = false,
            Pooling = false,
        };
        var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();
        return conn;
    }

    private static bool LooksLikeQuery(string sql)
    {
        string trimmed = sql.TrimStart();
        if (trimmed.Length == 0)
            return false;

        ReadOnlySpan<char> head = trimmed.AsSpan();
        while (head.Length > 0 && char.IsWhiteSpace(head[0]))
            head = head[1..];

        return head.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
               || head.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
               || head.StartsWith("SHOW", StringComparison.OrdinalIgnoreCase);
    }

    private static ResultSetResponse ReadResultSet(SqlDataReader reader)
    {
        var columns = new List<ResultColumn>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(BuildColumn(reader, i));

        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                values[i] = reader.IsDBNull(i) ? null : NormalizeValue(reader.GetValue(i));
            rows.Add(values);
        }

        return new ResultSetResponse(columns, rows);
    }

    private static ResultColumn BuildColumn(SqlDataReader reader, int ordinal)
    {
        string name = reader.GetName(ordinal);
        string dataType = reader.GetDataTypeName(ordinal);
        Type fieldType = reader.GetFieldType(ordinal);
        var schema = reader.GetColumnSchema()[ordinal];
        int length = schema.ColumnSize.GetValueOrDefault();
        byte precision = (byte)schema.NumericPrecision.GetValueOrDefault();
        byte scale = (byte)schema.NumericScale.GetValueOrDefault();
        bool nullable = schema.AllowDBNull.GetValueOrDefault(true);

        (ColumnType type, int colLength, int colPrecision, int colScale) =
            MapType(dataType, fieldType, length, precision, scale);
        return new ResultColumn(name, type, colLength, colPrecision, colScale, nullable);
    }

    private static (ColumnType Type, int Length, int Precision, int Scale) MapType(
        string dataType,
        Type fieldType,
        int length,
        byte precision,
        byte scale)
    {
        string t = dataType.ToLowerInvariant();
        return t switch
        {
            "bit" => (ColumnType.Integer, 0, 0, 0),
            "tinyint" => (ColumnType.Smallint, 0, 0, 0),
            "smallint" => (ColumnType.Smallint, 0, 0, 0),
            "int" => (ColumnType.Integer, 0, 0, 0),
            "bigint" => (ColumnType.Bigint, 0, 0, 0),
            "real" => (ColumnType.Real, 0, 0, 0),
            "float" => (ColumnType.Double, 0, 0, 0),
            "decimal" or "numeric" => (ColumnType.Decimal, 0, precision, scale),
            "money" => (ColumnType.Decimal, 0, 19, 4),
            "smallmoney" => (ColumnType.Decimal, 0, 10, 4),
            "char" => (ColumnType.Char, Math.Max(length, 1), 0, 0),
            "varchar" => (ColumnType.Varchar, Math.Max(length, 255), 0, 0),
            "nchar" => (ColumnType.Varchar, Math.Max(length, 1), 0, 0),
            "nvarchar" => (ColumnType.Varchar, Math.Max(length, 255), 0, 0),
            "binary" or "varbinary" => (ColumnType.Varchar, Math.Max(length, 100), 0, 0),
            "date" => (ColumnType.Date, 0, 0, 0),
            "time" => (ColumnType.Varchar, 16, 0, 0),
            "datetime" or "smalldatetime" => (ColumnType.Timestamp, 0, 0, 0),
            "datetime2" => (ColumnType.Timestamp, 0, 0, 7),
            "datetimeoffset" => (ColumnType.Timestamp, 0, 0, 0),
            "uniqueidentifier" => (ColumnType.Varchar, 36, 0, 0),
            _ when fieldType == typeof(bool) => (ColumnType.Integer, 0, 0, 0),
            _ when fieldType == typeof(byte) => (ColumnType.Smallint, 0, 0, 0),
            _ when fieldType == typeof(short) => (ColumnType.Smallint, 0, 0, 0),
            _ when fieldType == typeof(int) => (ColumnType.Integer, 0, 0, 0),
            _ when fieldType == typeof(long) => (ColumnType.Bigint, 0, 0, 0),
            _ when fieldType == typeof(float) => (ColumnType.Real, 0, 0, 0),
            _ when fieldType == typeof(double) => (ColumnType.Double, 0, 0, 0),
            _ when fieldType == typeof(decimal) => (ColumnType.Decimal, 0, precision, scale),
            _ when fieldType == typeof(DateTime) => (ColumnType.Timestamp, 0, 0, 0),
            _ when fieldType == typeof(DateTimeOffset) => (ColumnType.Timestamp, 0, 0, 0),
            _ when fieldType == typeof(TimeSpan) => (ColumnType.Time, 0, 0, 0),
            _ when fieldType == typeof(Guid) => (ColumnType.Varchar, 36, 0, 0),
            _ when fieldType == typeof(byte[]) => (ColumnType.Varchar, Math.Max(length, 100), 0, 0),
            _ => (ColumnType.Varchar, 255, 0, 0),
        };
    }

    private static object? NormalizeValue(object value) => value switch
    {
        bool b => b ? 1 : 0,
        byte b => (short)b,
        byte[] bytes => Convert.ToHexString(bytes),
        Guid g => g.ToString("D", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.UtcDateTime,
        TimeSpan ts => ts.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture),
        string s => s,
        _ => value,
    };
}
