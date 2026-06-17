namespace SizzlingDb.Core.Mapping;

/// <summary>Logical column type for configured fixed result sets.</summary>
internal enum ColumnType
{
    Char,
    Varchar,
    Nvarchar,
    Smallint,
    Integer,
    Bigint,
    Real,
    Double,
    Decimal,
    Money,
    SmallMoney,
    Guid,
    Bit,
    Date,
    Time,
    Timestamp,
}

internal static class ColumnTypeExtensions
{
    public static ColumnType Parse(string type) => type.Trim().ToUpperInvariant() switch
    {
        "CHAR" or "CHARACTER" => ColumnType.Char,
        "VARCHAR" or "VARCHAR2" or "STRING" => ColumnType.Varchar,
        "NCHAR" or "NVARCHAR" => ColumnType.Nvarchar,
        "SMALLINT" or "INT2" => ColumnType.Smallint,
        "INTEGER" or "INT" or "INT4" => ColumnType.Integer,
        "BIGINT" or "INT8" => ColumnType.Bigint,
        "REAL" or "FLOAT4" => ColumnType.Real,
        "DOUBLE" or "FLOAT" or "FLOAT8" => ColumnType.Double,
        "DECIMAL" or "NUMERIC" or "DEC" => ColumnType.Decimal,
        "MONEY" => ColumnType.Money,
        "SMALLMONEY" => ColumnType.SmallMoney,
        "UNIQUEIDENTIFIER" or "GUID" => ColumnType.Guid,
        "BIT" => ColumnType.Bit,
        "DATE" => ColumnType.Date,
        "TIME" => ColumnType.Time,
        "TIMESTAMP" or "DATETIME" or "DATETIME2" or "SMALLDATETIME" => ColumnType.Timestamp,
        _ => throw new InvalidOperationException($"Unsupported column type: {type}"),
    };

    public static bool IsCharacter(this ColumnType t) =>
        t is ColumnType.Char or ColumnType.Varchar or ColumnType.Nvarchar;
}
