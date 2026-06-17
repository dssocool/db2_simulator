using System.Buffers.Binary;
using System.Net.Sockets;

namespace SizzlingDb.Backends.SqlServer.Protocol;

internal sealed class TdsWriter
{
    private readonly Stream _stream;
    private readonly MemoryStream _buffer = new();
    private byte _packetId;

    public TdsWriter(Stream stream) => _stream = stream;

    public void Reset() => _buffer.SetLength(0);

    public void WriteByte(byte value) => _buffer.WriteByte(value);

    public void WriteBytes(ReadOnlySpan<byte> bytes) => _buffer.Write(bytes);

    public void WriteUInt16Le(ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        _buffer.Write(b);
    }

    public void WriteUInt32Le(uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        _buffer.Write(b);
    }

    public void WriteUnicode(string value)
    {
        foreach (char c in value)
        {
            WriteByte((byte)(c & 0xFF));
            WriteByte((byte)(c >> 8));
        }
    }

    public void WriteUnicodeZ(string value)
    {
        WriteUnicode(value);
        WriteByte(0);
        WriteByte(0);
    }

    public void Flush(byte packetType, byte status = TdsTypes.StatusEom)
    {
        byte[] payload = _buffer.ToArray();
        WritePacket(packetType, status, payload);
        Reset();
    }

    public void WritePacket(byte packetType, byte status, ReadOnlySpan<byte> payload)
    {
        int length = 8 + payload.Length;
        Span<byte> header = stackalloc byte[8];
        header[0] = packetType;
        header[1] = status;
        BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), (ushort)length);
        BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4, 2), 0);
        header[6] = ++_packetId;
        header[7] = 0;

        _stream.Write(header);
        if (!payload.IsEmpty)
            _stream.Write(payload);
        _stream.Flush();
    }
}
