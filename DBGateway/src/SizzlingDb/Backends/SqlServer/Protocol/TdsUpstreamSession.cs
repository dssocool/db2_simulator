using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using SizzlingDb.Config;

namespace SizzlingDb.Backends.SqlServer.Protocol;

/// <summary>
/// Maintains a logged-in TDS session to an upstream SQL Server and relays SQL batch
/// responses without re-encoding result sets.
/// </summary>
internal sealed class TdsUpstreamSession : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly TdsReader _reader;
    private readonly TdsWriter _writer;
    private readonly TraceLogger _log;

    private TdsUpstreamSession(TcpClient client, NetworkStream stream, TraceLogger log)
    {
        _client = client;
        _stream = stream;
        _log = log;
        _reader = new TdsReader(stream);
        _writer = new TdsWriter(stream);
    }

    public static TdsUpstreamSession Connect(SqlServerForwardConfig config, string database, TraceLogger log)
    {
        var client = new TcpClient();
        client.Connect(config.Host, config.Port > 0 ? config.Port : 1433);
        client.NoDelay = true;
        NetworkStream stream = client.GetStream();
        var session = new TdsUpstreamSession(client, stream, log);
        session.Handshake(config, database);
        return session;
    }

    private void Handshake(SqlServerForwardConfig config, string database)
    {
        SendPreLogin();
        ReadUntilResponse();
        SendLogin(config, database);
        ReadUntilLoginComplete();
    }

    public void RelaySqlBatch(ReadOnlySpan<byte> sqlBatchPayload, Stream clientStream)
    {
        _writer.WritePacket(TdsTypes.PacketSqlBatch, TdsTypes.StatusEom, sqlBatchPayload);
        while (_reader.ReadPacket())
        {
            WriteRawPacket(clientStream, _reader.LastType, _reader.LastStatus, _reader.LastPayload);
            if ((_reader.LastStatus & TdsTypes.StatusEom) != 0 && IsTerminalResponse(_reader.LastPayload))
                break;
        }
    }

    private static bool IsTerminalResponse(ReadOnlySpan<byte> payload)
    {
        foreach (byte token in payload)
        {
            if (token is TdsTypes.TokenDone or TdsTypes.TokenDoneProc or TdsTypes.TokenDoneInProc)
                return true;
            if (token is TdsTypes.TokenError)
                return true;
        }
        return false;
    }

    private static void WriteRawPacket(Stream stream, byte type, byte status, ReadOnlySpan<byte> payload)
    {
        int length = 8 + payload.Length;
        Span<byte> header = stackalloc byte[8];
        header[0] = type;
        header[1] = status;
        BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), (ushort)length);
        header[6] = 1;
        stream.Write(header);
        if (!payload.IsEmpty)
            stream.Write(payload);
        stream.Flush();
    }

    private void SendPreLogin()
    {
        byte[] version = [0x10, 0x00, 0x03, 0xE8, 0x00, 0x00];
        (byte Token, byte[] Value)[] entries =
        [
            (TdsTypes.PreLoginVersion, version),
            (TdsTypes.PreLoginEncryption, [TdsTypes.EncryptOff]),
            (TdsTypes.PreLoginInstOpt, [0x00]),
            (TdsTypes.PreLoginThreadId, []),
            (TdsTypes.PreLoginMars, [0x00]),
        ];

        using var body = new MemoryStream();
        int pos = entries.Length * 5 + 1;
        foreach ((byte token, byte[] value) in entries)
        {
            body.WriteByte(token);
            body.WriteByte((byte)(pos >> 8));
            body.WriteByte((byte)(pos & 0xFF));
            body.WriteByte((byte)(value.Length >> 8));
            body.WriteByte((byte)(value.Length & 0xFF));
            pos += value.Length;
        }
        body.WriteByte(0xFF);
        foreach ((_, byte[] value) in entries)
            body.Write(value);

        _writer.WritePacket(TdsTypes.PacketPreLogin, TdsTypes.StatusEom, body.ToArray());
    }

    private void SendLogin(SqlServerForwardConfig config, string database)
    {
        string db = string.IsNullOrWhiteSpace(database) ? config.Database : database;
        byte[] packet = BuildLogin7(config.User, config.Password, db);
        _writer.WritePacket(TdsTypes.PacketLogin, TdsTypes.StatusEom, packet);
    }

    private static byte[] BuildLogin7(string user, string password, string database)
    {
        const int headerLen = 94;
        string app = "sizzlingdb";
        string server = "";
        string language = "us_english";
        string lib = "sizzlingdb";

        byte[] userBytes = Unicode(user);
        byte[] passBytes = EncodePassword(password);
        byte[] dbBytes = Unicode(database);
        byte[] appBytes = Unicode(app);
        byte[] serverBytes = Unicode(server);
        byte[] langBytes = Unicode(language);
        byte[] libBytes = Unicode(lib);

        int total = headerLen + userBytes.Length + passBytes.Length + dbBytes.Length
                    + appBytes.Length + serverBytes.Length + langBytes.Length + libBytes.Length + 1;
        var buf = new byte[total];
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0, 4), total);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4, 4), TdsTypes.TdsVersion74);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8, 4), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(12, 4), 0x0007_0000);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(16, 4), (uint)Environment.ProcessId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(20, 4), 0);

        int offset = headerLen;
        WriteOffsetLength(buf, 40, ref offset, userBytes);
        WriteOffsetLength(buf, 44, ref offset, passBytes);
        WriteOffsetLength(buf, 48, ref offset, appBytes);
        WriteOffsetLength(buf, 52, ref offset, serverBytes);
        WriteOffsetLength(buf, 60, ref offset, langBytes);
        WriteOffsetLength(buf, 68, ref offset, dbBytes);
        WriteOffsetLength(buf, 72, ref offset, libBytes);
        buf[headerLen - 1] = 0xFF;
        return buf;
    }

    private static void WriteOffsetLength(byte[] buf, int fieldOffset, ref int dataOffset, byte[] data)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(fieldOffset, 2), (ushort)dataOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(fieldOffset + 2, 2), (ushort)(data.Length / 2));
        data.CopyTo(buf, dataOffset);
        dataOffset += data.Length;
    }

    private static byte[] Unicode(string value)
    {
        var bytes = new byte[value.Length * 2];
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bytes[i * 2] = (byte)(c & 0xFF);
            bytes[i * 2 + 1] = (byte)(c >> 8);
        }
        return bytes;
    }

    private static byte[] EncodePassword(string password)
    {
        var bytes = new byte[password.Length * 2];
        for (int i = 0; i < password.Length; i++)
        {
            byte b = (byte)(password[i] & 0xFF);
            byte swapped = (byte)((b >> 4) | (b << 4));
            bytes[i * 2] = (byte)(swapped ^ 0xA5);
            bytes[i * 2 + 1] = 0;
        }
        return bytes;
    }

    private void ReadUntilResponse()
    {
        while (_reader.ReadPacket())
        {
            if (_reader.LastType == TdsTypes.PacketResponse)
                return;
        }
        throw new InvalidOperationException("upstream pre-login failed");
    }

    private void ReadUntilLoginComplete()
    {
        while (_reader.ReadPacket())
        {
            if (_reader.LastType != TdsTypes.PacketResponse)
                continue;
            if (ContainsToken(_reader.LastPayload, TdsTypes.TokenLoginAck))
                return;
            if (ContainsToken(_reader.LastPayload, TdsTypes.TokenError))
                throw new InvalidOperationException("upstream login failed");
        }
        throw new InvalidOperationException("upstream login ended unexpectedly");
    }

    private static bool ContainsToken(ReadOnlySpan<byte> payload, byte token)
    {
        foreach (byte b in payload)
            if (b == token)
                return true;
        return false;
    }

    public void Dispose()
    {
        _stream.Dispose();
        _client.Dispose();
    }
}
