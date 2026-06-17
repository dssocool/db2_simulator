using SizzlingDb.Backends.SqlServer.Protocol;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.SqlServer;

internal static class SqlServerColumnTypes
{
    public static byte TdsType(this ColumnType t) => t switch
    {
        ColumnType.Char => TdsColumnTypes.TypeBigVarChar,
        ColumnType.Varchar => TdsColumnTypes.TypeBigVarChar,
        ColumnType.Smallint => TdsColumnTypes.TypeInt2,
        ColumnType.Integer => TdsColumnTypes.TypeInt4,
        ColumnType.Bigint => TdsColumnTypes.TypeInt8,
        ColumnType.Real => TdsColumnTypes.TypeFloat4,
        ColumnType.Double => TdsColumnTypes.TypeFloat8,
        ColumnType.Decimal => TdsColumnTypes.TypeDecimal,
        ColumnType.Date => TdsColumnTypes.TypeDate,
        ColumnType.Time => TdsColumnTypes.TypeDateTime2,
        ColumnType.Timestamp => TdsColumnTypes.TypeDateTime,
        _ => TdsColumnTypes.TypeNVarChar,
    };
}
