using System.Data.Common;
using System.Globalization;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// A fully materialized, normalized query result used to compare the same query
/// executed directly against SQL Server and through the gateway.
/// </summary>
internal sealed class ResultTable
{
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; }

    private ResultTable(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public static ResultTable Read(DbDataReader reader)
    {
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<IReadOnlyList<object?>>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = Normalize(reader.GetValue(i));
            rows.Add(row);
        }

        return new ResultTable(columns, rows);
    }

    public static ResultTable FromForwarder(ResultSetResponse response)
    {
        var columns = response.Columns.Select(c => c.Name).ToList();
        var rows = response.Rows
            .Select(row => row.Select(Normalize).ToList() as IReadOnlyList<object?>)
            .ToList();
        return new ResultTable(columns, rows);
    }

    public static void AssertEqual(ResultTable expected, ResultTable actual, string expectedLabel, string actualLabel)
    {
        Assert.True(
            expected.Columns.Count == actual.Columns.Count,
            $"Column count differs: {expectedLabel} returned {expected.Columns.Count} ({string.Join(", ", expected.Columns)}), " +
            $"{actualLabel} returned {actual.Columns.Count} ({string.Join(", ", actual.Columns)})");

        for (int c = 0; c < expected.Columns.Count; c++)
        {
            Assert.True(
                string.Equals(expected.Columns[c], actual.Columns[c], StringComparison.OrdinalIgnoreCase),
                $"Column {c} name differs: {expectedLabel} = '{expected.Columns[c]}', {actualLabel} = '{actual.Columns[c]}'");
        }

        Assert.True(
            expected.Rows.Count == actual.Rows.Count,
            $"Row count differs: {expectedLabel} returned {expected.Rows.Count}, {actualLabel} returned {actual.Rows.Count}");

        for (int r = 0; r < expected.Rows.Count; r++)
        {
            for (int c = 0; c < expected.Columns.Count; c++)
            {
                object? e = expected.Rows[r][c];
                object? a = actual.Rows[r][c];
                if (!Equals(e, a))
                {
                    Assert.Fail(
                        $"Row {r}, column {expected.Columns[c]}: " +
                        $"{expectedLabel} = {Format(e)}, {actualLabel} = {Format(a)}");
                }
            }
        }
    }

    private static object? Normalize(object? value) => value switch
    {
        null or DBNull => null,
        bool b => b ? 1m : 0m,
        string s when TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out TimeSpan ts) =>
            new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds),
        string s => s.TrimEnd(),
        byte b => (decimal)b,
        sbyte or short or ushort or int or uint or long or ulong or decimal => Convert.ToDecimal(value),
        float f => (double)f,
        double d => d,
        DateTimeOffset dto => dto.UtcDateTime,
        DateTime dt when dt.Kind == DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        DateTime dt => dt.ToUniversalTime(),
        TimeSpan ts => new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds),
        byte[] bytes => Convert.ToHexString(bytes),
        Guid g => g.ToString("D", CultureInfo.InvariantCulture),
        _ => value,
    };

    private static string Format(object? value) => value switch
    {
        null => "NULL",
        string s => $"'{s}'",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
        TimeSpan ts => ts.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "NULL",
    } + $" ({value?.GetType().Name ?? "null"})";
}
