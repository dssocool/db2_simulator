namespace SizzlingDb.Backends.Db2.Protocol;

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
