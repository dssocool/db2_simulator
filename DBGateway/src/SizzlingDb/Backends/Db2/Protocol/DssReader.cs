using System.Buffers.Binary;
using System.Net.Sockets;

namespace SizzlingDb.Backends.Db2.Protocol;

/// <summary>One DSS frame received from the client (a single DDM object).</summary>
internal sealed class DssFrame
{
    public required int Length { get; init; }
    public required int DssType { get; init; }
    public required bool Chained { get; init; }
    public required bool SameIdAsNext { get; init; }
    public required int CorrelationId { get; init; }
    public required int CodePoint { get; init; }
    public required byte[] Payload { get; init; } // bytes after the 4-byte DDM ll+cp

    public bool IsObject => DssType == CodePoints.DSSFMT_OBJDSS;
}

/// <summary>Reads DRDA DSS frames from a socket. Protocol framing is big-endian.</summary>
internal sealed class DssReader
{
    private readonly Stream _stream;
    private readonly List<byte> _rawChain = new(256);

    public DssReader(Stream stream) => _stream = stream;

    /// <summary>Raw bytes of the chain returned by the most recent <see cref="ReadChain"/>.</summary>
    public byte[] LastChainRaw => _rawChain.ToArray();

    /// <summary>
    /// Reads one full request chain: DSS frames until (and including) one whose
    /// chain bit is off. Returns null at end of stream.
    /// </summary>
    public List<DssFrame>? ReadChain()
    {
        _rawChain.Clear();
        var frames = new List<DssFrame>();
        while (true)
        {
            DssFrame? frame = ReadFrame();
            if (frame is null)
                return frames.Count == 0 ? null : frames;
            frames.Add(frame);
            if (!frame.Chained)
                return frames;
        }
    }

    private DssFrame? ReadFrame()
    {
        byte[]? header = ReadExact(6);
        if (header is null)
            return null;
        _rawChain.AddRange(header);

        int dssLen = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        if (header[2] != CodePoints.DSS_MAGIC)
            throw new IOException($"Invalid DSS magic byte 0x{header[2]:X2} (expected 0xD0).");

        byte flags = header[3];
        int dssType = flags & 0x0F;
        bool chained = (flags & CodePoints.DSS_CHAINED) != 0;
        bool sameId = (flags & CodePoints.DSS_SAME_ID) != 0;
        int corrId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));

        if (dssLen == 0xFFFF)
            throw new IOException("Continued (>32K) request DSS is not supported by the simulator.");
        if (dssLen < 6)
            throw new IOException($"DSS length {dssLen} too small.");

        byte[] body = ReadExact(dssLen - 6)
                      ?? throw new IOException("Unexpected end of stream reading DSS body.");
        _rawChain.AddRange(body);

        int codePoint = body.Length >= 4
            ? BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(2, 2))
            : 0;
        byte[] payload = body.Length > 4 ? body[4..] : Array.Empty<byte>();

        return new DssFrame
        {
            Length = dssLen,
            DssType = dssType,
            Chained = chained,
            SameIdAsNext = sameId,
            CorrelationId = corrId,
            CodePoint = codePoint,
            Payload = payload,
        };
    }

    /// <summary>Parse the nested DDM parameters within a command payload.</summary>
    public static List<(int CodePoint, byte[] Value)> ParseParameters(byte[] payload)
    {
        var list = new List<(int, byte[])>();
        int i = 0;
        while (i + 4 <= payload.Length)
        {
            int len = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(i, 2));
            int cp = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(i + 2, 2));
            if (len < 4 || i + len > payload.Length)
                break;
            byte[] value = payload[(i + 4)..(i + len)];
            list.Add((cp, value));
            i += len;
        }
        return list;
    }

    private byte[]? ReadExact(int count)
    {
        if (count == 0)
            return Array.Empty<byte>();
        var buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = _stream.Read(buf, read, count - read);
            if (n <= 0)
                return read == 0 ? null : throw new IOException("Unexpected end of stream.");
            read += n;
        }
        return buf;
    }
}
