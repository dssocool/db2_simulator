using SizzlingDb.Backends.Db2.Protocol;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.Db2;

internal static class Db2ColumnTypes
{
    /// <summary>Nullable FD:OCA type code for the QRYDSC/QRYDTA streams.</summary>
    public static int DrdaType(this ColumnType t) => t switch
    {
        ColumnType.Char => DrdaTypes.NVARMIX,
        ColumnType.Varchar => DrdaTypes.NVARCHAR,
        ColumnType.Smallint => DrdaTypes.NSMALL,
        ColumnType.Integer => DrdaTypes.NINTEGER,
        ColumnType.Bigint => DrdaTypes.NINTEGER8,
        ColumnType.Real => DrdaTypes.NFLOAT4,
        ColumnType.Double => DrdaTypes.NFLOAT8,
        ColumnType.Decimal => DrdaTypes.NDECIMAL,
        ColumnType.Date => DrdaTypes.NDATE,
        ColumnType.Time => DrdaTypes.NTIME,
        ColumnType.Timestamp => DrdaTypes.NTIMESTAMP,
        _ => throw new InvalidOperationException(),
    };

    /// <summary>DB2 SQL type code (nullable) for the SQLDARD descriptor.</summary>
    public static int SqlType(this ColumnType t) => t switch
    {
        ColumnType.Char => DrdaTypes.SQL_CHAR,
        ColumnType.Varchar => DrdaTypes.SQL_VARCHAR,
        ColumnType.Smallint => DrdaTypes.SQL_SMALL,
        ColumnType.Integer => DrdaTypes.SQL_INTEGER,
        ColumnType.Bigint => DrdaTypes.SQL_BIGINT,
        ColumnType.Real => DrdaTypes.SQL_FLOAT,
        ColumnType.Double => DrdaTypes.SQL_FLOAT,
        ColumnType.Decimal => DrdaTypes.SQL_DECIMAL,
        ColumnType.Date => DrdaTypes.SQL_DATE,
        ColumnType.Time => DrdaTypes.SQL_TIME,
        ColumnType.Timestamp => DrdaTypes.SQL_TIMESTAMP,
        _ => throw new InvalidOperationException(),
    };
}
