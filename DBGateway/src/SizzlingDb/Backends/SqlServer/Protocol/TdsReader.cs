using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace SizzlingDb.Backends.SqlServer.Protocol;

internal sealed class TdsReader
{
    private readonly Stream _stream;
    private readonly byte[] _header = new byte[8];
    private byte[] _payload = Array.Empty<byte>();

    public TdsReader(Stream stream) => _stream = stream;

    public byte LastType { get; private set; }
    public byte LastStatus { get; private set; }
    public ReadOnlySpan<byte> LastPayload => _payload;

    public bool ReadPacket()
    {
        if (!ReadExact(_header))
            return false;

        LastType = _header[0];
        LastStatus = _header[1];
        int length = BinaryPrimitives.ReadUInt16BigEndian(_header.AsSpan(2, 2));
        if (length < 8)
            throw new InvalidOperationException($"Invalid TDS packet length: {length}");

        int payloadLen = length - 8;
        if (_payload.Length < payloadLen)
            _payload = new byte[payloadLen];
        if (payloadLen > 0 && !ReadExact(_payload.AsSpan(0, payloadLen)))
            return false;
        return true;
    }

    public static Dictionary<byte, byte[]> ParsePreLogin(ReadOnlySpan<byte> payload)
    {
        var tokens = new Dictionary<byte, byte[]>();
        int i = 0;
        var offsets = new List<(byte Token, ushort Offset)>();
        while (i + 5 <= payload.Length)
        {
            byte token = payload[i];
            if (token == 0xFF)
                break;
            ushort offset = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(i + 1, 2));
            ushort length = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(i + 3, 2));
            offsets.Add((token, offset));
            i += 5;
        }

        foreach ((byte token, ushort offset) in offsets)
        {
            int start = offset;
            int end = start;
            while (end < payload.Length && payload[end] != 0)
                end++;
            int len = end - start;
            if (len > 0)
            {
                var value = payload.Slice(start, len).ToArray();
                tokens[token] = value;
            }
            else
            {
                tokens[token] = Array.Empty<byte>();
            }
        }

        return tokens;
    }

    public static Login7Data ParseLogin7(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 64)
            throw new InvalidOperationException("Login7 packet too short.");

        int ibUser = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(40, 2));
        int cchUser = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(42, 2));
        int ibPassword = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(44, 2));
        int cchPassword = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(46, 2));
        int ibApp = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(48, 2));
        int cchApp = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(50, 2));
        int ibDatabase = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(68, 2));
        int cchDatabase = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(70, 2));
        int loginLen = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(0, 4));
        ReadOnlySpan<byte> featureExt = ExtractFeatureExt(payload);

        string user = ReadUnicodeString(payload, ibUser, cchUser);
        string password = DecodePassword(payload, ibPassword, cchPassword);

        return new Login7Data
        {
            User = user,
            Password = password,
            Database = ReadUnicodeString(payload, ibDatabase, cchDatabase),
            FeatureExt = featureExt.ToArray(),
            HasFeatureExt = !featureExt.IsEmpty,
        };
    }

    public static ReadOnlySpan<byte> ExtractFeatureExt(ReadOnlySpan<byte> payload)
    {
        int start = FindFeatureExtStart(payload);
        if (start >= payload.Length)
            return ReadOnlySpan<byte>.Empty;

        int end = start;
        while (end < payload.Length)
        {
            if (payload[end] == 0xFF)
            {
                end++;
                break;
            }

            if (end + 5 > payload.Length)
                break;

            int dataLen = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(end + 1, 4));
            end += 5 + dataLen;
        }

        return end > start ? payload.Slice(start, end - start) : ReadOnlySpan<byte>.Empty;
    }

    private static int FindFeatureExtStart(ReadOnlySpan<byte> payload)
    {
        int end = 36;
        ReadOnlySpan<int> fieldOffsets =
        [
            36, 40, 44, 48, 52, 56, 60, 64, 68, 72, 76,
        ];

        foreach (int offset in fieldOffsets)
        {
            if (offset + 4 > payload.Length)
                break;

            int ib = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, 2));
            int cch = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset + 2, 2));
            if (ib < 0 || ib > payload.Length || cch < 0 || cch > 4096)
                continue;

            end = Math.Max(end, Math.Min(ib + cch * 2, payload.Length));
        }

        return end;
    }

    private static bool HasFeatureExtBlock(ReadOnlySpan<byte> payload, int loginLen)
    {
        _ = loginLen;
        return FindFeatureExtStart(payload) < payload.Length;
    }

    public static string ParseSqlBatch(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            return "";

        // MS-TDS SQL batch: first UINT32 is total AllHeaders length (includes itself).
        int sqlOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (sqlOffset < 4 || sqlOffset > payload.Length)
            sqlOffset = Math.Min(22, payload.Length);

        ReadOnlySpan<byte> sqlBytes = payload.Slice(sqlOffset);
        int end = sqlBytes.Length;
        for (int i = 0; i + 1 < sqlBytes.Length; i += 2)
        {
            if (sqlBytes[i] == 0 && sqlBytes[i + 1] == 0)
            {
                end = i;
                break;
            }
        }

        string sql = Encoding.Unicode.GetString(sqlBytes.Slice(0, end));
        // SqlClient may append client context after the statement without a null separator.
        int cut = sql.Length;
        for (int i = 0; i < sql.Length; i++)
        {
            if (sql[i] > 127)
            {
                cut = i;
                break;
            }
        }

        return sql[..cut].Trim();
    }

    private static string ReadUnicodeString(ReadOnlySpan<byte> payload, int byteOffset, int charCount)
    {
        if (charCount <= 0 || byteOffset < 0 || byteOffset >= payload.Length)
            return "";
        int byteLen = Math.Min(charCount * 2, payload.Length - byteOffset);
        return Encoding.Unicode.GetString(payload.Slice(byteOffset, byteLen)).TrimEnd('\0');
    }

    private static string DecodePassword(ReadOnlySpan<byte> payload, int byteOffset, int charCount)
    {
        if (charCount <= 0 || byteOffset < 0 || byteOffset >= payload.Length)
            return "";

        var sb = new StringBuilder();
        for (int i = 0; i < charCount; i++)
        {
            int pos = byteOffset + i * 2;
            if (pos >= payload.Length)
                break;
            byte encoded = payload[pos];
            if (encoded == 0)
                break;
            byte x = (byte)(encoded ^ 0xA5);
            byte unswapped = (byte)((x >> 4) | (x << 4));
            sb.Append((char)unswapped);
        }
        return sb.ToString();
    }

    private bool ReadExact(Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = _stream.Read(buffer.Slice(read));
            if (n == 0)
                return false;
            read += n;
        }
        return true;
    }
}

internal sealed class Login7Data
{
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public string Database { get; init; } = "";
    public byte[] FeatureExt { get; init; } = [];
    public bool HasFeatureExt { get; init; }

    public IEnumerable<(string User, string Password)> CredentialCandidates()
    {
        yield return (User, Password);
    }
}
