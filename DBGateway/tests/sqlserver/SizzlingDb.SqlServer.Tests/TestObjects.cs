namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Names and seed data for objects provisioned by Test02_Setup and consumed by
/// Test03_ForwarderComparison against the real SQL Server database.
/// </summary>
internal static class TestObjects
{
    public const string SqlServerDatabase = "SIZZLINGDB_TESTS";

    public const string TableName = "SIZZLINGDB_TYPES";

    public const string AllColumns =
        "ID, COL_BIT, COL_TINYINT, COL_SMALLINT, COL_INT, COL_BIGINT, " +
        "COL_REAL, COL_FLOAT, COL_DECIMAL, COL_NUMERIC, COL_MONEY, COL_SMALLMONEY, " +
        "COL_CHAR, COL_VARCHAR, COL_NCHAR, COL_NVARCHAR, " +
        "COL_BINARY, COL_VARBINARY, " +
        "COL_DATE, COL_TIME, COL_DATETIME, COL_DATETIME2, COL_SMALLDATETIME, COL_DATETIMEOFFSET, " +
        "COL_UNIQUEIDENTIFIER";

    public static readonly Guid Row1Guid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Row2Guid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly byte[] Row1Binary = [0xDE, 0xAD, 0xBE, 0xEF];
    public static readonly byte[] Row2Binary = [0x01, 0x02, 0x03, 0x04, 0x05];

    public static readonly IReadOnlyList<TypeSampleRow> Rows =
    [
        new(
            1,
            true,
            255,
            (short)-2,
            42,
            9_007_199_254_740_993L,
            1.5f,
            1.25,
            12345.6789m,
            99.99m,
            19.99m,
            0.99m,
            "AB",
            "Widget",
            "CD",
            "Gadget",
            Row1Binary,
            Row2Binary,
            new DateTime(2026, 1, 15),
            new TimeSpan(0, 9, 30, 15, 123),
            new DateTime(2026, 6, 10, 14, 30, 0),
            new DateTime(2026, 6, 10, 14, 30, 0).AddTicks(1_234_560),
            new DateTime(2026, 6, 10, 14, 30, 0),
            new DateTimeOffset(2026, 6, 10, 14, 30, 0, TimeSpan.FromHours(-5)),
            Row1Guid),
        new(
            2,
            false,
            0,
            (short)300,
            -1,
            -5_000_000_000L,
            -2.25f,
            -0.5,
            0.01m,
            0m,
            0.01m,
            0m,
            "EFGHI",
            "Gizmo",
            "JKLMN",
            "Sprocket",
            [0x00, 0x00, 0x00, 0x00],
            [0xFF],
            new DateTime(1999, 2, 28),
            new TimeSpan(0, 23, 59, 59, 999),
            new DateTime(2000, 1, 1),
            new DateTime(2000, 1, 1, 0, 0, 0).AddTicks(9_999_990),
            new DateTime(2000, 1, 1),
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)),
            Row2Guid),
        new(
            3,
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null, null, null,
            null),
    ];
}

internal sealed record TypeSampleRow(
    int Id,
    bool? ColBit,
    byte? ColTinyint,
    short? ColSmallint,
    int? ColInt,
    long? ColBigint,
    float? ColReal,
    double? ColFloat,
    decimal? ColDecimal,
    decimal? ColNumeric,
    decimal? ColMoney,
    decimal? ColSmallmoney,
    string? ColChar,
    string? ColVarchar,
    string? ColNchar,
    string? ColNvarchar,
    byte[]? ColBinary,
    byte[]? ColVarbinary,
    DateTime? ColDate,
    TimeSpan? ColTime,
    DateTime? ColDatetime,
    DateTime? ColDatetime2,
    DateTime? ColSmalldatetime,
    DateTimeOffset? ColDatetimeoffset,
    Guid? ColUniqueidentifier);
