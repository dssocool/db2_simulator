using System.Buffers.Binary;

namespace SizzlingDb.Backends.Db2.Protocol;

/// <summary>
/// Builds a single DDM object (length + code point + nested content). Protocol
/// framing fields are big-endian; FD:OCA "data" fields follow the negotiated
/// type-definition endianness (little for QTDSQLX86, big for QTDSQLASC).
/// </summary>
internal sealed class Ddm
{
    private readonly List<byte> _buf = new(64);
    private readonly Stack<int> _marks = new();

    public bool LittleData { get; }

    public Ddm(bool littleData) => LittleData = littleData;

    public int Length => _buf.Count;

    public byte[] ToArray() => _buf.ToArray();

    // ---- DDM collection framing ----

    public Ddm Begin(int codePoint)
    {
        _marks.Push(_buf.Count);
        WriteU16BE(0); // length placeholder
        WriteU16BE(codePoint);
        return this;
    }

    public Ddm End()
    {
        int start = _marks.Pop();
        int len = _buf.Count - start;
        if (len > 0x7FFF)
            throw new InvalidOperationException(
                $"DDM object too large ({len} bytes); the simulator does not split DSS blocks.");
        _buf[start] = (byte)(len >> 8);
        _buf[start + 1] = (byte)len;
        return this;
    }

    // ---- raw / framing (big-endian) ----

    public Ddm WriteByte(int b)
    {
        _buf.Add((byte)b);
        return this;
    }

    public Ddm WriteU16BE(int v)
    {
        _buf.Add((byte)(v >> 8));
        _buf.Add((byte)v);
        return this;
    }

    public Ddm WriteU32BE(long v)
    {
        _buf.Add((byte)(v >> 24));
        _buf.Add((byte)(v >> 16));
        _buf.Add((byte)(v >> 8));
        _buf.Add((byte)v);
        return this;
    }

    public Ddm WriteBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
            _buf.Add(b);
        return this;
    }

    // ---- FD:OCA data values (endianness depends on type definition) ----

    public Ddm WriteI16Data(int v)
    {
        Span<byte> s = stackalloc byte[2];
        if (LittleData) BinaryPrimitives.WriteInt16LittleEndian(s, (short)v);
        else BinaryPrimitives.WriteInt16BigEndian(s, (short)v);
        return WriteBytes(s);
    }

    public Ddm WriteI32Data(int v)
    {
        Span<byte> s = stackalloc byte[4];
        if (LittleData) BinaryPrimitives.WriteInt32LittleEndian(s, v);
        else BinaryPrimitives.WriteInt32BigEndian(s, v);
        return WriteBytes(s);
    }

    public Ddm WriteI64Data(long v)
    {
        Span<byte> s = stackalloc byte[8];
        if (LittleData) BinaryPrimitives.WriteInt64LittleEndian(s, v);
        else BinaryPrimitives.WriteInt64BigEndian(s, v);
        return WriteBytes(s);
    }

    public Ddm WriteF32Data(float v)
    {
        Span<byte> s = stackalloc byte[4];
        if (LittleData) BinaryPrimitives.WriteSingleLittleEndian(s, v);
        else BinaryPrimitives.WriteSingleBigEndian(s, v);
        return WriteBytes(s);
    }

    public Ddm WriteF64Data(double v)
    {
        Span<byte> s = stackalloc byte[8];
        if (LittleData) BinaryPrimitives.WriteDoubleLittleEndian(s, v);
        else BinaryPrimitives.WriteDoubleBigEndian(s, v);
        return WriteBytes(s);
    }

    // ---- scalar DDM parameter helpers ----

    public Ddm Scalar1(int codePoint, int value)
    {
        WriteU16BE(5);
        WriteU16BE(codePoint);
        WriteByte(value);
        return this;
    }

    public Ddm Scalar2(int codePoint, int value)
    {
        WriteU16BE(6);
        WriteU16BE(codePoint);
        WriteU16BE(value);
        return this;
    }

    public Ddm ScalarBytes(int codePoint, ReadOnlySpan<byte> value)
    {
        WriteU16BE(value.Length + 4);
        WriteU16BE(codePoint);
        WriteBytes(value);
        return this;
    }

    /// <summary>EBCDIC-encoded protocol string parameter.</summary>
    public Ddm ScalarString(int codePoint, string value)
        => ScalarBytes(codePoint, Ccsid.Ebcdic.GetBytes(value));
}
