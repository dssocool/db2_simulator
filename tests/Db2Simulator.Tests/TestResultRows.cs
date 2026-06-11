using System.Text.Json;
using Db2Simulator.Config;

namespace Db2Simulator.Tests;

internal static class TestResultRows
{
    public static ResultConfig Result(IReadOnlyList<ColumnConfig> columns, params object?[][] rows) =>
        new()
        {
            Columns = columns.ToList(),
            Rows = rows.Select(row => row.Select(v => JsonSerializer.SerializeToElement(v)).ToList()).ToList(),
        };
}
