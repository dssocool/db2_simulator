namespace Db2Simulator.Protocol;

/// <summary>
/// FD:OCA DRDA type codes (nullable variants are odd, non-nullable even = nullable-1)
/// and the corresponding DB2 SQL type codes used in SQLDARD.
/// </summary>
internal static class DrdaTypes
{
    // Nullable FD:OCA type codes.
    public const int NSMALL = 0x05;
    public const int NINTEGER = 0x03;
    public const int NINTEGER8 = 0x17; // BIGINT
    public const int NFLOAT4 = 0x0D;   // REAL
    public const int NFLOAT8 = 0x0B;   // DOUBLE
    public const int NDECIMAL = 0x0F;
    public const int NDATE = 0x21;
    public const int NTIME = 0x23;
    public const int NTIMESTAMP = 0x25;
    public const int NCHAR = 0x31;
    public const int NVARCHAR = 0x33;
    public const int NVARMIX = 0x3F;   // variable character mixed (used for CHAR)
    public const int NLONGMIX = 0x41;

    // DB2 SQL type codes (nullable variants are odd).
    public const int SQL_SMALL = 501;
    public const int SQL_INTEGER = 497;
    public const int SQL_BIGINT = 493;
    public const int SQL_FLOAT = 481;     // REAL/DOUBLE both 480/481
    public const int SQL_DECIMAL = 485;
    public const int SQL_DATE = 385;
    public const int SQL_TIME = 389;
    public const int SQL_TIMESTAMP = 393;
    public const int SQL_CHAR = 453;
    public const int SQL_VARCHAR = 449;
}

/// <summary>Logical column type for the simulator's fixed result sets.</summary>
internal enum SimColumnType
{
    Char,
    Varchar,
    Smallint,
    Integer,
    Bigint,
    Real,
    Double,
    Decimal,
    Date,
    Time,
    Timestamp,
}

internal static class SimColumnTypeExtensions
{
    public static SimColumnType Parse(string type) => type.Trim().ToUpperInvariant() switch
    {
        "CHAR" or "CHARACTER" => SimColumnType.Char,
        "VARCHAR" or "VARCHAR2" or "STRING" => SimColumnType.Varchar,
        "SMALLINT" or "INT2" => SimColumnType.Smallint,
        "INTEGER" or "INT" or "INT4" => SimColumnType.Integer,
        "BIGINT" or "INT8" => SimColumnType.Bigint,
        "REAL" or "FLOAT4" => SimColumnType.Real,
        "DOUBLE" or "FLOAT" or "FLOAT8" => SimColumnType.Double,
        "DECIMAL" or "NUMERIC" or "DEC" => SimColumnType.Decimal,
        "DATE" => SimColumnType.Date,
        "TIME" => SimColumnType.Time,
        "TIMESTAMP" or "DATETIME" => SimColumnType.Timestamp,
        _ => throw new InvalidOperationException($"Unsupported column type: {type}"),
    };

    /// <summary>Nullable FD:OCA type code for the QRYDSC/QRYDTA streams.</summary>
    public static int DrdaType(this SimColumnType t) => t switch
    {
        SimColumnType.Char => DrdaTypes.NVARMIX,
        SimColumnType.Varchar => DrdaTypes.NVARCHAR,
        SimColumnType.Smallint => DrdaTypes.NSMALL,
        SimColumnType.Integer => DrdaTypes.NINTEGER,
        SimColumnType.Bigint => DrdaTypes.NINTEGER8,
        SimColumnType.Real => DrdaTypes.NFLOAT4,
        SimColumnType.Double => DrdaTypes.NFLOAT8,
        SimColumnType.Decimal => DrdaTypes.NDECIMAL,
        SimColumnType.Date => DrdaTypes.NDATE,
        SimColumnType.Time => DrdaTypes.NTIME,
        SimColumnType.Timestamp => DrdaTypes.NTIMESTAMP,
        _ => throw new InvalidOperationException(),
    };

    /// <summary>DB2 SQL type code (nullable) for the SQLDARD descriptor.</summary>
    public static int SqlType(this SimColumnType t) => t switch
    {
        SimColumnType.Char => DrdaTypes.SQL_CHAR,
        SimColumnType.Varchar => DrdaTypes.SQL_VARCHAR,
        SimColumnType.Smallint => DrdaTypes.SQL_SMALL,
        SimColumnType.Integer => DrdaTypes.SQL_INTEGER,
        SimColumnType.Bigint => DrdaTypes.SQL_BIGINT,
        SimColumnType.Real => DrdaTypes.SQL_FLOAT,
        SimColumnType.Double => DrdaTypes.SQL_FLOAT,
        SimColumnType.Decimal => DrdaTypes.SQL_DECIMAL,
        SimColumnType.Date => DrdaTypes.SQL_DATE,
        SimColumnType.Time => DrdaTypes.SQL_TIME,
        SimColumnType.Timestamp => DrdaTypes.SQL_TIMESTAMP,
        _ => throw new InvalidOperationException(),
    };

    public static bool IsCharacter(this SimColumnType t) =>
        t is SimColumnType.Char or SimColumnType.Varchar;
}
