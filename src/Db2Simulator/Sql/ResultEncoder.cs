using System.Globalization;
using System.Text;
using Db2Simulator.Protocol;

namespace Db2Simulator.Sql;

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

    public byte[] BuildSqldard(IReadOnlyList<ResultColumn> columns, string database)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.SQLDARD);

        // DB2OLEDB (and pydrda's db2 parser) expect the DB2 LUW describe layout:
        // a 79-byte SQLCAGRP, a separator 0xFF, a 6-byte SQLNUMROW prefix, then
        // SQLDAGRP entries that carry the alias in SQLLABEL (SQLNAME is empty).
        WriteSqlcaForDescribe(d, database);
        d.WriteByte(0xFF);
        d.WriteI16Data(columns.Count);
        d.WriteI32Data(0);

        foreach (ResultColumn c in columns)
            WriteSqldaGroupOleDb(d, c);

        return d.End().ToArray();
    }

    // 79-byte SQLCAGRP prefix captured from DB2 LUW; only PRDID and RDBNAM vary.
    private void WriteSqlcaForDescribe(Ddm d, string database)
    {
        Span<byte> header = stackalloc byte[79];
        SqlcaDescribeTemplate().CopyTo(header);
        _sqlerrproc.CopyTo(header[10..18]);

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

    private void WriteSqldaGroupOleDb(Ddm d, ResultColumn c)
    {
        int sqlType = c.Type.SqlType() & 0xFFFE;
        int ccsid = UsesDescribeCcsid(c.Type) ? Ccsid.Utf8 : 0;
        byte[] label = Encoding.UTF8.GetBytes(c.Name);

        d.WriteI16Data(SqlPrecisionField(c));
        d.WriteI16Data(c.Type == SimColumnType.Decimal ? c.Scale : 0);
        d.WriteI32Data(0);
        d.WriteI16Data(sqlType);
        d.WriteU16BE(ccsid);
        d.WriteBytes(new byte[14]);
        d.WriteByte(label.Length);
        d.WriteBytes(label);
        d.WriteBytes(new byte[12 - label.Length]);
        d.WriteBytes([0xFF, 0xFF, 0xFF]);
    }

    private static int SqlPrecisionField(ResultColumn c) => c.Type switch
    {
        SimColumnType.Timestamp => 26,
        SimColumnType.Date => 10,
        SimColumnType.Time => 8,
        SimColumnType.Char or SimColumnType.Varchar => c.Length,
        SimColumnType.Decimal => c.Precision,
        SimColumnType.Smallint => 2,
        SimColumnType.Integer => 4,
        SimColumnType.Bigint => 8,
        _ => 0,
    };

    private static bool UsesDescribeCcsid(SimColumnType t) =>
        t.IsCharacter() || t is SimColumnType.Timestamp or SimColumnType.Date or SimColumnType.Time;

    private static string PadRdbnam(string name) => name.Length >= 18 ? name : name.PadRight(18);

    // ---------------- QRYDSC (answer set descriptor) ----------------

    public byte[] BuildQrydsc(IReadOnlyList<ResultColumn> columns)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.QRYDSC);

        int groupLen = 3 + 3 * columns.Count;
        d.WriteByte(groupLen);  // SQLDTAGRP length byte
        d.WriteByte(0x76);      // N-GDA triplet type
        d.WriteByte(0xD0);      // SQLDTAGRP LID

        foreach (ResultColumn c in columns)
        {
            d.WriteByte(c.Type.DrdaType() & 0xFE);
            if (c.Type == SimColumnType.Decimal)
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

        // End-of-data marker: full SQLCA with SQLSTATE 02000 / SQLCODE +100.
        WriteSqlcaFull(d, 100, "02000", null, rows.Count, 0);
        d.WriteByte(0xFF); // trailing data group: none

        return d.End().ToArray();
    }

    private void WriteValue(Ddm d, ResultColumn c, object? value)
    {
        // QRYDSC advertises the non-nullable FD:OCA types (0x24/0x21/0x23), so fixed-
        // length date/time/timestamp values are sent without a per-column null indicator.
        if (c.Type is SimColumnType.Date or SimColumnType.Time or SimColumnType.Timestamp)
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
            case SimColumnType.Char:
            case SimColumnType.Varchar:
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                d.WriteU16BE(bytes.Length).WriteBytes(bytes);
                break;
            }
            case SimColumnType.Smallint:
                d.WriteI16Data((int)(long)value);
                break;
            case SimColumnType.Integer:
                d.WriteI32Data((int)(long)value);
                break;
            case SimColumnType.Bigint:
                d.WriteI64Data((long)value);
                break;
            case SimColumnType.Real:
                d.WriteF32Data((float)(double)value);
                break;
            case SimColumnType.Double:
                d.WriteF64Data((double)value);
                break;
            case SimColumnType.Decimal:
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
        SimColumnType.Char or SimColumnType.Varchar => c.Length,
        SimColumnType.Smallint => 2,
        SimColumnType.Integer => 4,
        SimColumnType.Bigint => 8,
        SimColumnType.Real => 4,
        SimColumnType.Double => 8,
        SimColumnType.Decimal => (c.Precision << 8) | (c.Scale & 0xFF),
        SimColumnType.Date => 10,
        SimColumnType.Time => 8,
        SimColumnType.Timestamp => 26,
        _ => 0,
    };

    private static int QrydscLength(ResultColumn c) => c.Type switch
    {
        SimColumnType.Char or SimColumnType.Varchar => Math.Min(c.Length, 0x7FFF),
        SimColumnType.Smallint => 2,
        SimColumnType.Integer => 4,
        SimColumnType.Bigint => 8,
        SimColumnType.Real => 4,
        SimColumnType.Double => 8,
        SimColumnType.Date => 10,
        SimColumnType.Time => 8,
        SimColumnType.Timestamp => 26,
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
