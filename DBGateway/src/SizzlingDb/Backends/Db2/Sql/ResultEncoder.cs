using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SizzlingDb.Backends.Db2;
using SizzlingDb.Backends.Db2.Protocol;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.Db2.Sql;

/// <summary>
/// Encodes FD:OCA reply structures (SQLCARD, SQLDARD, QRYDSC, QRYDTA) for the
/// DB2 LUW (SQLAM level 7, QTDSQLX86) dialect. Numeric "data" fields use the
/// negotiated endianness; framing fields are always big-endian.
/// </summary>
internal sealed class ResultEncoder
{
    // SQLCADTA + SQLDTARD FD:OCA descriptor footer (from DRDA/Derby).
    private static readonly byte[] SqlcadtaSqldtardRlo =
    {
        0x09, 0x71, 0xE0, 0x54, 0x00, 0x01,
        0xD0, 0x00, 0x01, 0x06, 0x71, 0xF0,
        0xE0, 0x00, 0x00,
    };

    private readonly bool _littleData;
    private readonly byte[] _sqlerrproc; // 8 bytes

    public ResultEncoder(bool littleData, string productId)
    {
        _littleData = littleData;
        _sqlerrproc = Encoding.ASCII.GetBytes((productId + "        ")[..8]);
    }

    // ---------------- SQLCARD ----------------

    public byte[] BuildSqlcardSuccess()
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLCARD);
        WriteSqlcaFull(d, 0, "     ", null, 0, 0);
        return d.End().ToArray();
    }

    public byte[] BuildSqlcardEndOfData(long rowCount, string database)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLCARD);
        WriteSqlcaEof(d, rowCount, database);
        return d.End().ToArray();
    }

    public byte[] BuildSqlcardError(int sqlcode, string sqlstate, string message)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLCARD);
        WriteSqlcaFull(d, sqlcode, sqlstate, message, 0, 0);
        return d.End().ToArray();
    }

    public byte[] BuildSqlcardUpdate(long updateCount)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLCARD);
        WriteSqlcaFull(d, 0, "     ", null, 0, updateCount);
        return d.End().ToArray();
    }

    // ---------------- SQLDARD (describe output) ----------------

    public byte[] BuildSqldard(IReadOnlyList<ResultColumn> columns, string database, bool compact = false)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLDARD);

        // DB2 LUW describe: 79-byte SQLCAGRP, 0xFF separator, SQLNUMROW prefix, SQLDAGRP chunks.
        // DB2OLEDB (SQL Server OPENQUERY) uses a shorter per-column label layout than pydrda/DRDA tools.
        WriteSqlcaForDescribe(d, database, columns.Count);
        d.WriteByte(0xFF);
        d.WriteI16Data(columns.Count);
        d.WriteI32Data(0);

        for (int i = 0; i < columns.Count; i++)
            d.WriteBytes(BuildSqldaColumnChunk(columns[i], i, compact));

        return d.End().ToArray();
    }

    // 79-byte SQLCAGRP prefix captured from DB2 LUW; only PRDID and RDBNAM vary.
    private void WriteSqlcaForDescribe(Ddm d, string database, int columnCount)
    {
        Span<byte> header = stackalloc byte[79];
        SqlcaDescribeTemplate().CopyTo(header);
        _sqlerrproc.CopyTo(header[10..18]);

        if (columnCount >= 1)
        {
            // DB2 LUW describe uses a fixed SQLCAXGRP tail (0x0E at bytes 28-31).
            BinaryPrimitives.WriteInt32BigEndian(header[28..32], 14);
            header[32..40].Clear();
        }

        byte[] rdb = Encoding.ASCII.GetBytes(PadRdbnam(database));
        header[54] = 0;
        header[55] = (byte)rdb.Length;
        rdb.CopyTo(header[56..(56 + rdb.Length)]);

        d.WriteBytes(header);
    }

    private static ReadOnlySpan<byte> SqlcaDescribeTemplate() =>
    [
        0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x30, 0x30, 0x30,
        0x53, 0x51, 0x4C, 0x31, 0x31, 0x30, 0x35, 0x35, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x21, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00,
        0x00, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0x12, 0x54, 0x45,
        0x53, 0x54, 0x44, 0x42, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x00, 0x00, 0x00, 0x00, 0xFF,
    ];

    /// <summary>
    /// Builds one SQLDAGRP chunk exactly as consumed by pydrda/DB2OLEDB for DB2 LUW describe.
    /// Layout captured from a live DB2 LUW server (OPENQUERY with long column aliases).
    /// </summary>
    private byte[] BuildSqldaColumnChunk(ResultColumn c, int columnIndex, bool compact)
    {
        var buf = new List<byte>(64);
        byte[] label = Encoding.UTF8.GetBytes(c.Name);

        if (compact)
            WriteOleDbCompactColumnHeader(buf, c, columnIndex);
        else
            WriteExtendedColumnHeader(buf, c, columnIndex);

        if (compact)
        {
            buf.AddRange(new byte[12]);
            buf.Add((byte)label.Length);
            buf.AddRange(label);
            buf.AddRange(new byte[10]);
            buf.AddRange([0xFF, 0xFF]);
            return buf.ToArray();
        }

        if (c.Type == ColumnType.Decimal)
        {
            buf.AddRange(new byte[14]);
            buf.Add((byte)label.Length);
            buf.AddRange(label);
            buf.AddRange(new byte[Math.Max(0, 26 - label.Length)]);
        }
        else
        {
            buf.AddRange(new byte[8]);
            buf.AddRange([0x08, 0x00, 0x00, 0x00, 0x00, 0x00]);
            buf.Add((byte)label.Length);
            buf.AddRange(label);
            buf.AddRange(new byte[10]);
        }

        buf.AddRange([0xFF, 0xFF, 0xFF]);
        return buf.ToArray();
    }

    private void WriteExtendedColumnHeader(List<byte> buf, ResultColumn c, int columnIndex)
    {
        if (columnIndex > 0 && c.Type is not ColumnType.Decimal)
            buf.AddRange(new byte[4]);

        AppendI16Data(buf, SqlPrecisionField(c));
        AppendI16Data(buf, c.Type == ColumnType.Decimal ? c.Scale : 0);
        buf.AddRange(new byte[c.Type == ColumnType.Decimal ? 8 : 4]);
        AppendI16Data(buf, c.Type.SqlType());
        int ccsid = UsesDescribeCcsid(c.Type) ? Ccsid.Utf8 : 0;
        buf.Add((byte)(ccsid >> 8));
        buf.Add((byte)ccsid);
    }

    private void WriteOleDbCompactColumnHeader(List<byte> buf, ResultColumn c, int columnIndex)
    {
        int ccsid = UsesDescribeCcsid(c.Type) ? Ccsid.Utf8 : 0;
        if (columnIndex > 0 && c.Type is not ColumnType.Decimal)
        {
            buf.AddRange(new byte[4]);
            AppendI32Data(buf, SqlPrecisionField(c));
            AppendI32Data(buf, 0);
        }
        else
        {
            AppendI16Data(buf, SqlPrecisionField(c));
            AppendI16Data(buf, c.Type == ColumnType.Decimal ? c.Scale : 0);
            buf.AddRange(new byte[c.Type == ColumnType.Decimal ? 8 : 4]);
        }

        AppendI16Data(buf, c.Type.SqlType());
        buf.Add((byte)(ccsid >> 8));
        buf.Add((byte)ccsid);
    }

    private void AppendI32Data(List<byte> buf, int value)
    {
        Span<byte> s = stackalloc byte[4];
        if (_littleData)
            BinaryPrimitives.WriteInt32LittleEndian(s, value);
        else
            BinaryPrimitives.WriteInt32BigEndian(s, value);
        buf.Add(s[0]);
        buf.Add(s[1]);
        buf.Add(s[2]);
        buf.Add(s[3]);
    }

    private void AppendI16Data(List<byte> buf, int value)
    {
        Span<byte> s = stackalloc byte[2];
        if (_littleData)
            BinaryPrimitives.WriteInt16LittleEndian(s, (short)value);
        else
            BinaryPrimitives.WriteInt16BigEndian(s, (short)value);
        buf.Add(s[0]);
        buf.Add(s[1]);
    }

    private static int SqlPrecisionField(ResultColumn c) => c.Type switch
    {
        ColumnType.Timestamp => 26,
        ColumnType.Date => 10,
        ColumnType.Time => 8,
        ColumnType.Char or ColumnType.Varchar => c.Length,
        ColumnType.Decimal => c.Precision,
        ColumnType.Smallint => 2,
        ColumnType.Integer => 4,
        ColumnType.Bigint => 8,
        _ => 0,
    };

    private static bool UsesDescribeCcsid(ColumnType t) =>
        t.IsCharacter() || t is ColumnType.Timestamp or ColumnType.Date or ColumnType.Time;

    private static string PadRdbnam(string name) => name.Length >= 18 ? name : name.PadRight(18);

    private void WriteSqlcaEof(Ddm d, long rowCount, string database)
    {
        d.WriteByte(0x00);
        d.WriteI32Data(100);
        d.WriteBytes(Fixed5("02000"));
        d.WriteBytes(Encoding.ASCII.GetBytes("SQLRI01F"));
        d.WriteByte(0x00);
        d.WriteI32Data(0x00000480);
        d.WriteI32Data(1);
        d.WriteI32Data(0);
        d.WriteI32Data(0);
        d.WriteBytes(new byte[8]);
        d.WriteBytes(Encoding.ASCII.GetBytes("           "));
        byte[] rdb = Encoding.ASCII.GetBytes(PadRdbnam(database));
        d.WriteU16BE(rdb.Length).WriteBytes(rdb);
        d.WriteU16BE(0).WriteU16BE(0);
        d.WriteByte(0xFF);
    }

    // ---------------- QRYDSC (answer set descriptor) ----------------

    public byte[] BuildQrydsc(IReadOnlyList<ResultColumn> columns, bool oleDbClient = false)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.QRYDSC);

        int groupLen = 3 + 3 * columns.Count;
        d.WriteByte(groupLen);  // SQLDTAGRP length byte
        d.WriteByte(0x76);      // N-GDA triplet type
        d.WriteByte(0xD0);      // SQLDTAGRP LID

        foreach (ResultColumn c in columns)
        {
            int drdaType = c.Type == ColumnType.Char ? c.Type.DrdaType() & 0xFD : c.Type.DrdaType();
            d.WriteByte(drdaType);
            if (c.Type == ColumnType.Decimal)
            {
                d.WriteByte(c.Precision);
                d.WriteByte(c.Scale);
            }
            else
            {
                d.WriteU16BE(QrydscLength(c)); // length override (big-endian)
            }
        }

        d.WriteBytes(SqlcadtaSqldtardRlo);
        return d.End().ToArray();
    }

    // ---------------- QRYDTA (answer set data) ----------------

    public byte[] BuildQrydta(IReadOnlyList<ResultColumn> columns, IReadOnlyList<object?[]> rows)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.QRYDTA);

        foreach (object?[] row in rows)
        {
            d.WriteByte(0xFF); // per-row SQLCAGRP: null (no per-row diagnostics)
            d.WriteByte(0x00); // QRYDTA data group: present
            for (int i = 0; i < columns.Count; i++)
                WriteValue(d, columns[i], i < row.Length ? row[i] : null);
        }

        return d.End().ToArray();
    }

    private void WriteValue(Ddm d, ResultColumn c, object? value)
    {
        // QRYDSC advertises the non-nullable FD:OCA types (0x24/0x21/0x23), so fixed-
        // length date/time/timestamp values are sent without a per-column null indicator.
        if (c.Type is ColumnType.Date or ColumnType.Time or ColumnType.Timestamp)
        {
            string s = value is null
                ? new string(' ', QrydscLength(c))
                : PadFixed(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", QrydscLength(c));
            d.WriteBytes(Encoding.ASCII.GetBytes(s));
            return;
        }

        if (value is null)
        {
            d.WriteByte(0xFF); // null indicator
            return;
        }

        d.WriteByte(0x00); // not null

        switch (c.Type)
        {
            case ColumnType.Char:
            {
                // QRYDSC advertises fixed-length NVARMIX (0x3E); data is padded CHAR bytes.
                string s = PadFixed(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", c.Length);
                d.WriteBytes(Encoding.UTF8.GetBytes(s));
                break;
            }
            case ColumnType.Varchar:
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                d.WriteU16BE(bytes.Length).WriteBytes(bytes);
                break;
            }
            case ColumnType.Smallint:
                d.WriteI16Data((int)(long)value);
                break;
            case ColumnType.Integer:
                d.WriteI32Data((int)(long)value);
                break;
            case ColumnType.Bigint:
                d.WriteI64Data((long)value);
                break;
            case ColumnType.Real:
                d.WriteF32Data((float)(double)value);
                break;
            case ColumnType.Double:
                d.WriteF64Data((double)value);
                break;
            case ColumnType.Decimal:
                d.WriteBytes(PackDecimal((decimal)value, c.Precision, c.Scale));
                break;
            default:
                throw new InvalidOperationException($"Unsupported column type {c.Type}");
        }
    }

    // ---------------- SQLCA helpers ----------------

    private void WriteSqlcaFull(Ddm d, int sqlcode, string sqlstate, string? errmc, long rowCount, long updateCount)
    {
        d.WriteByte(0x00);                       // SQLCAGRP indicator present
        d.WriteI32Data(sqlcode);                 // SQLCODE
        d.WriteBytes(Fixed5(sqlstate));          // SQLSTATE (5 bytes)
        d.WriteBytes(_sqlerrproc);               // SQLERRPROC (8 bytes)

        // SQLCAXGRP (level 7)
        d.WriteByte(0x00);                       // indicator present
        d.WriteI32Data((int)(rowCount >>> 32));  // SQLERRD1
        d.WriteI32Data((int)(rowCount & 0xFFFFFFFFL)); // SQLERRD2
        d.WriteI32Data((int)(updateCount & 0xFFFFFFFFL)); // SQLERRD3
        d.WriteI32Data((int)(updateCount >>> 32)); // SQLERRD4
        d.WriteBytes(new byte[8]);               // SQLERRD5/D6
        d.WriteBytes(Encoding.ASCII.GetBytes("           ")); // WARN0..WARNA (11 spaces)
        d.WriteU16BE(0);                         // RDBNAM (VCS length 0)

        // SQLERRMC (VCM/VCS)
        if (string.IsNullOrEmpty(errmc))
        {
            d.WriteU16BE(0).WriteU16BE(0);
        }
        else
        {
            byte[] m = Encoding.UTF8.GetBytes(errmc);
            d.WriteU16BE(m.Length).WriteBytes(m).WriteU16BE(0);
        }

        d.WriteByte(0xFF);                       // SQLDIAGGRP null
    }

    // ---------------- value sizing helpers ----------------

    private static int SqlLength(ResultColumn c) => c.Type switch
    {
        ColumnType.Char or ColumnType.Varchar => c.Length,
        ColumnType.Smallint => 2,
        ColumnType.Integer => 4,
        ColumnType.Bigint => 8,
        ColumnType.Real => 4,
        ColumnType.Double => 8,
        ColumnType.Decimal => (c.Precision << 8) | (c.Scale & 0xFF),
        ColumnType.Date => 10,
        ColumnType.Time => 8,
        ColumnType.Timestamp => 26,
        _ => 0,
    };

    private static int QrydscLength(ResultColumn c) => c.Type switch
    {
        ColumnType.Char or ColumnType.Varchar => Math.Min(c.Length, 0x7FFF),
        ColumnType.Smallint => 2,
        ColumnType.Integer => 4,
        ColumnType.Bigint => 8,
        ColumnType.Real => 4,
        ColumnType.Double => 8,
        ColumnType.Date => 10,
        ColumnType.Time => 8,
        ColumnType.Timestamp => 26,
        _ => 0,
    };

    private static string PadFixed(string s, int length)
        => s.Length >= length ? s[..length] : s.PadRight(length, ' ');

    private static byte[] Fixed5(string s)
    {
        byte[] b = Encoding.ASCII.GetBytes((s + "     ")[..5]);
        return b;
    }

    private static byte[] PackDecimal(decimal value, int precision, int scale)
    {
        int encodedLength = precision / 2 + 1;
        int digitCount = 2 * encodedLength - 1;

        bool negative = value < 0;
        decimal abs = Math.Abs(value);
        string s = abs.ToString("F" + scale, CultureInfo.InvariantCulture).Replace(".", "");
        if (s.Length > digitCount)
            s = s[^digitCount..]; // overflow: keep least-significant digits
        s = s.PadLeft(digitCount, '0');

        var nibbles = new byte[2 * encodedLength];
        for (int i = 0; i < digitCount; i++)
            nibbles[i] = (byte)(s[i] - '0');
        nibbles[digitCount] = (byte)(negative ? 0x0D : 0x0C);

        var outb = new byte[encodedLength];
        for (int k = 0; k < encodedLength; k++)
            outb[k] = (byte)((nibbles[2 * k] << 4) | nibbles[2 * k + 1]);
        return outb;
    }
}
