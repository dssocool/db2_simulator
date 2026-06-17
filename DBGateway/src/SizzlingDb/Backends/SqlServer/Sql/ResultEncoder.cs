using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SizzlingDb.Backends.SqlServer.Protocol;
using SizzlingDb.Config;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.SqlServer.Sql;

/// <summary>Encodes TDS tabular result tokens (COLMETADATA, ROW, DONE, ERROR).</summary>
internal sealed class ResultEncoder
{
    private readonly SqlServerBackendConfig _backend;

    public ResultEncoder(SqlServerBackendConfig backend) => _backend = backend;

    public byte[] BuildLoginResponse(string database, ReadOnlySpan<byte> clientFeatureExt)
    {
        _ = database;
        _ = clientFeatureExt;
        return CapturedLoginResponse.Payload;
    }

    public byte[] BuildFeatureExtAckPacket() => [];

    private static void WriteLoginAckExact(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenLoginAck);
        w.WriteUInt16Le(54);
        ReadOnlySpan<byte> body =
        [
            0x01, 0x74, 0x00, 0x00, 0x04, 0x16, 0x4D, 0x00, 0x69, 0x00, 0x63, 0x00, 0x72, 0x00, 0x6F, 0x00,
            0x73, 0x00, 0x6F, 0x00, 0x66, 0x00, 0x74, 0x00, 0x20, 0x00, 0x53, 0x00, 0x51, 0x00, 0x4C, 0x00,
            0x20, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x10, 0x00, 0x03, 0xE8,
        ];
        w.WriteBytes(body);
    }

    private static void WriteDoneInProc(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenDoneInProc);
        w.WriteUInt16Le(0);
        w.WriteUInt16Le(0);
        w.WriteUInt32Le(0);
    }

    public byte[] BuildLoginError(string message)
    {
        var w = new TokenWriter();
        WriteError(w, 18456, 14, 1, message);
        WriteDone(w, 0, 0);
        return w.ToArray();
    }

    public byte[] BuildResultSet(ResultSetResponse result)
    {
        var w = new TokenWriter();
        WriteColMetadata(w, result.Columns);
        foreach (object?[] row in result.Rows)
            WriteRow(w, result.Columns, row);
        WriteDone(w, (ushort)result.Rows.Count, 0);
        return w.ToArray();
    }

    public byte[] BuildUpdateCount(long count)
    {
        var w = new TokenWriter();
        WriteDone(w, 0, (ushort)Math.Min(count, ushort.MaxValue));
        return w.ToArray();
    }

    public byte[] BuildError(int number, string message, string state = "42000")
    {
        var w = new TokenWriter();
        byte stateByte = state.Length >= 2 ? byte.Parse(state.AsSpan(0, 2)) : (byte)0;
        byte classByte = state.Length >= 5 ? byte.Parse(state.AsSpan(2, 3)) : (byte)16;
        WriteError(w, number, classByte, stateByte, message);
        WriteDone(w, 0, 0);
        return w.ToArray();
    }

    private void WriteEnvChangeDatabase(TokenWriter w, string database)
    {
        w.WriteByte(TdsTypes.TokenEnvChange);
        using var body = new MemoryStream();
        body.WriteByte(TdsTypes.EnvDatabase);
        WriteUnicodeCharCount(body, database);
        WriteUnicodeCharCount(body, database);
        byte[] data = body.ToArray();
        w.WriteUInt16Le((ushort)data.Length);
        w.WriteBytes(data);
    }

    private void WriteEnvChangeLanguage(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenEnvChange);
        using var body = new MemoryStream();
        body.WriteByte(TdsTypes.EnvLanguage);
        WriteUnicodeCharCount(body, "us_english");
        WriteUnicodeCharCount(body, "us_english");
        byte[] data = body.ToArray();
        w.WriteUInt16Le((ushort)data.Length);
        w.WriteBytes(data);
    }

    private void WriteEnvChangePacketSize(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenEnvChange);
        using var body = new MemoryStream();
        body.WriteByte(0x04); // packet size
        WriteUnicodeCharCount(body, "4096");
        WriteUnicodeCharCount(body, "4096");
        byte[] data = body.ToArray();
        w.WriteUInt16Le((ushort)data.Length);
        w.WriteBytes(data);
    }

    private void WriteLoginAck(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenLoginAck);
        using var body = new MemoryStream();
        body.WriteByte(0x01); // SQL interface
        body.WriteByte(0x74);
        body.WriteByte(0x00);
        body.WriteByte(0x00);
        body.WriteByte(0x04); // TDS 7.4
        string progName = PadProgName(_backend.ProductName);
        body.WriteByte((byte)progName.Length);
        WriteUnicodeFixed(body, progName);
        body.WriteByte(0x10);
        body.WriteByte(0x00);
        body.WriteByte(0x03);
        body.WriteByte(0xE8);
        byte[] data = body.ToArray();
        w.WriteUInt16Le((ushort)data.Length);
        w.WriteBytes(data);
    }

    private static string PadProgName(string name)
    {
        if (name.Length >= 22)
            return name[..22];
        return name.PadRight(22, '\0');
    }

    private static void WriteUnicodeCharCount(Stream body, string value)
    {
        body.WriteByte((byte)value.Length);
        WriteUnicodeFixed(body, value);
    }

    private static void WriteUnicodeFixed(Stream body, string value)
    {
        foreach (char c in value)
        {
            body.WriteByte((byte)(c & 0xFF));
            body.WriteByte((byte)(c >> 8));
        }
    }

    private static void WriteLoginDone(TokenWriter w)
    {
        w.WriteByte(TdsTypes.TokenDone);
        w.WriteUInt16Le(0);
        w.WriteUInt16Le(0);
        w.WriteUInt32Le(0);
    }

    private static void WriteError(TokenWriter w, int number, byte severity, byte state, string message)
    {
        w.WriteByte(TdsTypes.TokenError);
        byte[] data = BuildErrorBody(number, severity, state, message);
        w.WriteUInt16Le((ushort)data.Length);
        w.WriteBytes(data);
    }

    private static byte[] BuildErrorBody(int number, byte severity, byte state, string message)
    {
        using var body = new MemoryStream();
        body.WriteByte((byte)Math.Min(message.Length, 255));
        body.WriteByte(severity);
        body.WriteByte(state);
        foreach (char c in message)
        {
            body.WriteByte((byte)(c & 0xFF));
            body.WriteByte((byte)(c >> 8));
        }
        body.WriteByte(0);
        body.WriteByte(0);
        body.WriteByte(0);
        body.WriteByte(0);
        Span<byte> line = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(line, 0);
        body.Write(line);
        Span<byte> num = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(num, (uint)number);
        body.Write(num);
        return body.ToArray();
    }

    private static void WriteColMetadata(TokenWriter w, IReadOnlyList<ResultColumn> columns)
    {
        w.WriteByte(TdsTypes.TokenColMetadata);
        w.WriteUInt16Le((ushort)columns.Count);
        foreach (ResultColumn col in columns)
            WriteColumnMeta(w, col);
    }

    private static void WriteColumnMeta(TokenWriter w, ResultColumn col)
    {
        w.WriteUInt32Le(0); // UserType
        w.WriteByte(0x00); // padding (matches real SQL Server COLMETADATA)
        w.WriteByte(0x00);
        w.WriteUInt16Le(0x0020); // Computed column flag
        byte type = col.Type.TdsType();
        w.WriteByte(type);
        WriteTypeInfo(w, col, type);
        byte nameLen = (byte)Math.Min(col.Name.Length, 255);
        w.WriteByte(nameLen);
        foreach (char c in col.Name.AsSpan(0, nameLen))
        {
            w.WriteByte((byte)(c & 0xFF));
            w.WriteByte((byte)(c >> 8));
        }
    }

    private static void WriteTypeInfo(TokenWriter w, ResultColumn col, byte type)
    {
        switch (type)
        {
            case TdsColumnTypes.TypeDateTime2:
                w.WriteByte(7);
                break;
            case TdsColumnTypes.TypeNVarChar:
            case TdsColumnTypes.TypeBigVarChar:
                ushort maxLen = (ushort)(col.Length > 0 ? col.Length * 2 : 8000);
                w.WriteByte((byte)(maxLen & 0xFF));
                w.WriteByte((byte)(maxLen >> 8));
                break;
            case TdsColumnTypes.TypeDecimal:
                w.WriteByte(17);
                w.WriteByte((byte)col.Precision);
                w.WriteByte((byte)col.Scale);
                break;
            default:
                break;
        }
    }

    private static void WriteRow(TokenWriter w, IReadOnlyList<ResultColumn> columns, object?[] row)
    {
        w.WriteByte(TdsTypes.TokenRow);
        using var body = new MemoryStream();
        for (int i = 0; i < columns.Count; i++)
            WriteCell(body, columns[i], i < row.Length ? row[i] : null);
        w.WriteBytes(body.ToArray());
    }

    private static void WriteCell(Stream body, ResultColumn col, object? value)
    {
        if (value is null)
        {
            if (IsFixedLength(col.Type))
            {
                int len = FixedLength(col.Type);
                for (int i = 0; i < len; i++)
                    body.WriteByte(0);
            }
            else
            {
                body.WriteByte(0xFF);
                body.WriteByte(0xFF);
            }
            return;
        }

        switch (col.Type)
        {
            case ColumnType.Smallint:
                short s = Convert.ToInt16(value, CultureInfo.InvariantCulture);
                body.WriteByte((byte)(s & 0xFF));
                body.WriteByte((byte)(s >> 8));
                break;
            case ColumnType.Integer:
                Span<byte> ib = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(ib, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                body.Write(ib);
                break;
            case ColumnType.Bigint:
                Span<byte> lb = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(lb, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                body.Write(lb);
                break;
            case ColumnType.Real:
                Span<byte> fb = stackalloc byte[4];
                BinaryPrimitives.WriteSingleLittleEndian(fb, Convert.ToSingle(value, CultureInfo.InvariantCulture));
                body.Write(fb);
                break;
            case ColumnType.Double:
                Span<byte> db = stackalloc byte[8];
                BinaryPrimitives.WriteDoubleLittleEndian(db, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                body.Write(db);
                break;
            case ColumnType.Timestamp:
                WriteDateTime(body, ParseTimestamp(value));
                break;
            case ColumnType.Date:
                WriteDate(body, ParseDate(value));
                break;
            case ColumnType.Char:
            case ColumnType.Varchar:
                WriteVarCharCell(body, value.ToString() ?? "");
                break;
            case ColumnType.Decimal:
                WriteDecimalCell(body, Convert.ToDecimal(value, CultureInfo.InvariantCulture), col.Precision, col.Scale);
                break;
            default:
                WriteNVarCharCell(body, value.ToString() ?? "");
                break;
        }
    }

    private static bool IsFixedLength(ColumnType type) => type is
        ColumnType.Smallint or ColumnType.Integer or ColumnType.Bigint
        or ColumnType.Real or ColumnType.Double or ColumnType.Date;

    private static int FixedLength(ColumnType type) => type switch
    {
        ColumnType.Smallint => 2,
        ColumnType.Integer or ColumnType.Real => 4,
        ColumnType.Bigint or ColumnType.Double => 8,
        ColumnType.Date => 3,
        _ => 0,
    };

    private static void WriteDateTime(Stream body, DateTime dt)
    {
        // DATETIME (0x3D): 2-byte days + 2-byte pad + 4-byte 1/300-second ticks (8 bytes total)
        int days = (int)(dt.Date - new DateTime(1900, 1, 1)).TotalDays;
        int time = (int)(dt.TimeOfDay.TotalSeconds * 300);
        body.WriteByte((byte)(days & 0xFF));
        body.WriteByte((byte)(days >> 8));
        body.WriteByte(0);
        body.WriteByte(0);
        body.WriteByte((byte)(time & 0xFF));
        body.WriteByte((byte)((time >> 8) & 0xFF));
        body.WriteByte((byte)((time >> 16) & 0xFF));
        body.WriteByte((byte)((time >> 24) & 0xFF));
    }

    private static void WriteDateTime2(Stream body, DateTime dt)
    {
        body.WriteByte(8);
        long scaled = dt.TimeOfDay.Ticks / 100;
        Span<byte> time = stackalloc byte[5];
        time[0] = (byte)(scaled & 0xFF);
        time[1] = (byte)((scaled >> 8) & 0xFF);
        time[2] = (byte)((scaled >> 16) & 0xFF);
        time[3] = (byte)((scaled >> 24) & 0xFF);
        time[4] = (byte)((scaled >> 32) & 0xFF);
        body.Write(time);
        int days = (int)(dt.Date - new DateTime(1900, 1, 1)).TotalDays;
        Span<byte> date = stackalloc byte[3];
        date[0] = (byte)(days & 0xFF);
        date[1] = (byte)((days >> 8) & 0xFF);
        date[2] = (byte)((days >> 16) & 0xFF);
        body.Write(date);
    }

    private static void WriteDate(Stream body, DateTime dt)
    {
        int days = (int)(dt.Date - new DateTime(1900, 1, 1)).TotalDays;
        Span<byte> b = stackalloc byte[3];
        b[0] = (byte)(days & 0xFF);
        b[1] = (byte)((days >> 8) & 0xFF);
        b[2] = (byte)((days >> 16) & 0xFF);
        body.Write(b);
    }

    private static void WriteVarCharCell(Stream body, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        body.WriteByte((byte)(bytes.Length & 0xFF));
        body.WriteByte((byte)(bytes.Length >> 8));
        body.Write(bytes);
    }

    private static void WriteNVarCharCell(Stream body, string value)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(value);
        body.WriteByte((byte)(bytes.Length & 0xFF));
        body.WriteByte((byte)(bytes.Length >> 8));
        body.Write(bytes);
    }

    private static void WriteDecimalCell(Stream body, decimal value, int precision, int scale)
    {
        body.WriteByte(17);
        Span<byte> dec = stackalloc byte[17];
        EncodeDecimal(dec, value, precision, scale);
        body.Write(dec);
    }

    private static void EncodeDecimal(Span<byte> dest, decimal value, int precision, int scale)
    {
        dest.Clear();
        dest[0] = 1;
        dest[1] = (byte)scale;
        dest[2] = 1;
        dest[3] = (byte)precision;
        int[] bits = decimal.GetBits(value);
        dest[4] = (byte)(bits[3] >> 16);
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(5, 4), bits[0]);
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(9, 4), bits[1]);
        BinaryPrimitives.WriteInt32LittleEndian(dest.Slice(13, 4), bits[2]);
    }

    private static DateTime ParseTimestamp(object value) => value switch
    {
        DateTime dt => dt,
        string s => DateTime.ParseExact(
            s.Replace('.', '-'),
            ["yyyy-MM-dd-HH-mm-ss-ffffff", "yyyy-MM-dd-HH-mm-ss", "yyyy-MM-dd HH:mm:ss.fff"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None),
        _ => DateTime.UtcNow,
    };

    private static DateTime ParseDate(object value) => value switch
    {
        DateTime dt => dt.Date,
        string s => DateTime.Parse(s, CultureInfo.InvariantCulture).Date,
        _ => DateTime.UtcNow.Date,
    };

    private static void WriteDone(TokenWriter w, ushort rowCount, ushort curCmd)
    {
        _ = curCmd;
        w.WriteByte(TdsTypes.TokenDone);
        w.WriteUInt16Le(0x0010);
        w.WriteUInt16Le(0x00C1);
        Span<byte> count = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(count, rowCount);
        w.WriteBytes(count);
    }

    private static void WriteBVarChar(Stream body, string value)
    {
        byte len = (byte)Math.Min(value.Length, 255);
        body.WriteByte(len);
        body.Write(Encoding.UTF8.GetBytes(value[..len]));
    }

    private static void WriteUnicodeZ(Stream body, string value)
    {
        foreach (char c in value)
        {
            body.WriteByte((byte)(c & 0xFF));
            body.WriteByte((byte)(c >> 8));
        }
        body.WriteByte(0);
        body.WriteByte(0);
    }

    private sealed class TokenWriter
    {
        private readonly MemoryStream _stream = new();

        public void WriteByte(byte b) => _stream.WriteByte(b);

        public void WriteUInt16Le(ushort v)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(b, v);
            _stream.Write(b);
        }

        public void WriteUInt32Le(uint v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(b, v);
            _stream.Write(b);
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes) => _stream.Write(bytes);

        public byte[] ToArray() => _stream.ToArray();
    }
}
