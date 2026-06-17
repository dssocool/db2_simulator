using System.Globalization;
using System.Text.Json;
using SizzlingDb.Config;

namespace SizzlingDb.Core.Mapping;

internal abstract record StatementResponse;

internal sealed record ResultColumn(
    string Name,
    ColumnType Type,
    int Length,
    int Precision,
    int Scale,
    bool Nullable);

internal sealed record ResultSetResponse(
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<object?[]> Rows) : StatementResponse;

internal sealed record UpdateCountResponse(long Count) : StatementResponse;

internal sealed record ErrorResponse(int SqlCode, string SqlState, string Message) : StatementResponse;

/// <summary>Resolves an incoming SQL statement to its configured fixed response.</summary>
internal sealed class StatementMapper
{
    private readonly SizzlingDbConfig _config;

    public StatementMapper(SizzlingDbConfig config) => _config = config;

    public StatementResponse Resolve(string sql)
    {
        string normalized = Normalize(sql);

        foreach (MappingConfig m in _config.Mappings)
        {
            bool matched = m.Match switch
            {
                MatchKind.Regex => m.CompiledRegex!.IsMatch(sql.Trim()) || m.CompiledRegex!.IsMatch(normalized),
                _ => string.Equals(Normalize(m.Sql), normalized, StringComparison.Ordinal),
            };
            if (matched)
                return Build(m.Result, m.UpdateCount, m.Error);
        }

        if (_config.DefaultResponse is { } def)
            return Build(def.Result, def.UpdateCount, def.Error);

        return new ErrorResponse(-204, "42704",
            $"SIZZLINGDB: no mapping configured for statement: {sql.Trim()}");
    }

    private string Normalize(string sql)
    {
        string s = sql.Trim();
        if (_config.Matching.TrimTrailingSemicolon)
            s = s.TrimEnd().TrimEnd(';').TrimEnd();
        if (_config.Matching.CollapseWhitespace)
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        if (_config.Matching.IgnoreCase)
            s = s.ToUpperInvariant();
        return s;
    }

    private static StatementResponse Build(ResultConfig? result, long? updateCount, ErrorConfig? error)
    {
        if (error is not null)
            return new ErrorResponse(error.Sqlcode, error.Sqlstate, error.Message);
        if (updateCount is { } uc)
            return new UpdateCountResponse(uc);
        if (result is not null)
            return BuildResultSet(result);
        return new UpdateCountResponse(0);
    }

    private static ResultSetResponse BuildResultSet(ResultConfig result)
    {
        var columns = new List<ResultColumn>(result.Columns.Count);
        foreach (ColumnConfig c in result.Columns)
        {
            ColumnType type = ColumnTypeExtensions.Parse(c.Type);
            int length = c.Length ?? DefaultLength(type);
            int precision = c.Precision ?? (type == ColumnType.Decimal ? 9 : 0);
            int scale = c.Scale ?? (type == ColumnType.Decimal ? 2 : 0);
            columns.Add(new ResultColumn(c.Name, type, length, precision, scale, c.Nullable));
        }

        var rows = new List<object?[]>(result.Rows.Count);
        foreach (List<JsonElement> row in result.Rows)
        {
            var values = new object?[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                JsonElement cell = i < row.Count ? row[i] : default;
                values[i] = ConvertCell(cell, columns[i].Type);
            }
            rows.Add(values);
        }

        return new ResultSetResponse(columns, rows);
    }

    private static int DefaultLength(ColumnType t) => t switch
    {
        ColumnType.Char => 1,
        ColumnType.Varchar => 255,
        ColumnType.Timestamp => 26,
        ColumnType.Date => 10,
        ColumnType.Time => 8,
        _ => 0,
    };

    private static object? ConvertCell(JsonElement cell, ColumnType type)
    {
        if (cell.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return type switch
        {
            ColumnType.Char or ColumnType.Varchar
                or ColumnType.Date or ColumnType.Time or ColumnType.Timestamp
                => CellToString(cell),
            ColumnType.Smallint or ColumnType.Integer or ColumnType.Bigint
                => CellToLong(cell),
            ColumnType.Real or ColumnType.Double
                => CellToDouble(cell),
            ColumnType.Decimal
                => CellToDecimal(cell),
            _ => CellToString(cell),
        };
    }

    private static string CellToString(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.String => cell.GetString() ?? "",
        JsonValueKind.Number => cell.GetRawText(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        _ => cell.GetRawText(),
    };

    private static long CellToLong(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.Number => cell.TryGetInt64(out long l) ? l : (long)cell.GetDouble(),
        JsonValueKind.String => long.Parse(cell.GetString()!, CultureInfo.InvariantCulture),
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        _ => 0,
    };

    private static double CellToDouble(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.Number => cell.GetDouble(),
        JsonValueKind.String => double.Parse(cell.GetString()!, CultureInfo.InvariantCulture),
        _ => 0,
    };

    private static decimal CellToDecimal(JsonElement cell) => cell.ValueKind switch
    {
        JsonValueKind.Number => cell.GetDecimal(),
        JsonValueKind.String => decimal.Parse(cell.GetString()!, CultureInfo.InvariantCulture),
        _ => 0m,
    };
}
