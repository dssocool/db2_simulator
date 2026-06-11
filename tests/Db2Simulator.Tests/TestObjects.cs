namespace Db2Simulator.Tests;

/// <summary>
/// Names and seed data for the objects provisioned by Test02_Setup and consumed
/// by Test03_QueryComparison. Both servers receive the same logical rows so
/// results can be compared across SQL Server, the linked server, and DB2.
/// </summary>
internal static class TestObjects
{
    /// <summary>Linked server created in SQL Server, pointing at the real DB2 (tests.db2).</summary>
    public const string LinkedServerName = "DB2SIM_TESTLINK";

    /// <summary>Database created in SQL Server that holds the mirror copy of the DB2 test data.</summary>
    public const string SqlServerDatabase = "DB2SIM_TESTS";

    /// <summary>
    /// Test table name. Left unqualified in DB2 SQL so it resolves to the connecting
    /// user's default schema both directly and through the linked server.
    /// </summary>
    public const string TableName = "DB2SIM_SAMPLE";

    public const string AllColumns = "ID, NAME, CODE, SMALL_VAL, BIG_VAL, PRICE, RATIO, BORN, WAKES, CREATED";

    // RATIO values are kept exactly representable in binary floating point so the
    // same literal produces identical doubles in DB2 and SQL Server.
    public static readonly IReadOnlyList<SampleRow> Rows =
    [
        new(1, "Widget", "AB", 1, 1_000_000_000_000L, 19.99m, 1.5,
            new DateTime(2026, 1, 15), new TimeSpan(9, 30, 0),
            new DateTime(2026, 6, 10, 14, 30, 0).AddTicks(1_234_560)),
        new(2, "Gadget", "CD", -2, -5_000_000_000L, 0.01m, -2.25,
            new DateTime(2025, 12, 31), new TimeSpan(23, 59, 59),
            new DateTime(2025, 12, 31, 23, 59, 59).AddTicks(9_999_990)),
        new(3, "Gizmo", "EFGHI", 300, 9_007_199_254_740_993L, 12345.67m, 0.5,
            new DateTime(1999, 2, 28), TimeSpan.Zero,
            new DateTime(2000, 1, 1)),
        new(4, null, null, null, null, null, null, null, null, null),
    ];
}

internal sealed record SampleRow(
    int Id,
    string? Name,
    string? Code,
    short? SmallVal,
    long? BigVal,
    decimal? Price,
    double? Ratio,
    DateTime? Born,
    TimeSpan? Wakes,
    DateTime? Created);
