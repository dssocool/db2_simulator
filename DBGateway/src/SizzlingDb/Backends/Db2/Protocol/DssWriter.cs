using System.Buffers.Binary;

namespace SizzlingDb.Backends.Db2.Protocol;

/// <summary>
/// Accumulates reply DDM objects and flushes them as a chained sequence of DSS
/// frames. All replies for one request chain share the buffer; the final DSS in
/// the buffer has its chain bit cleared before the whole batch is sent.
/// </summary>
internal sealed class DssWriter
{
    private readonly Stream _stream;
    private readonly List<Frame> _frames = new();

    private readonly record struct Frame(int CorrId, int DssType, byte[] Ddm);

    public DssWriter(Stream stream) => _stream = stream;

    public int PendingCount => _frames.Count;

    public void AddReply(int corrId, byte[] ddm) => _frames.Add(new Frame(corrId, CodePoints.DSSFMT_RPYDSS, ddm));

    public void AddObject(int corrId, byte[] ddm) => _frames.Add(new Frame(corrId, CodePoints.DSSFMT_OBJDSS, ddm));

    /// <summary>Serialize all buffered frames into one chained batch and send it.</summary>
    public byte[] Flush()
    {
        var output = new List<byte>(256);
        var header = new byte[6];
        for (int i = 0; i < _frames.Count; i++)
        {
            Frame f = _frames[i];
            bool isLast = i == _frames.Count - 1;
            bool nextSameId = !isLast && _frames[i + 1].CorrId == f.CorrId;

            int dssLen = f.Ddm.Length + 6;
            if (dssLen > 0x7FFF)
                throw new InvalidOperationException(
                    $"Reply DSS too large ({dssLen} bytes). Reduce the configured row count for this statement.");

            int flags = f.DssType;
            if (!isLast) flags |= CodePoints.DSS_CHAINED;
            if (nextSameId) flags |= CodePoints.DSS_SAME_ID;

            BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)dssLen);
            header[2] = CodePoints.DSS_MAGIC;
            header[3] = (byte)flags;
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)f.CorrId);

            output.AddRange(header);
            output.AddRange(f.Ddm);
        }

        byte[] bytes = output.ToArray();
        _frames.Clear();
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();
        return bytes;
    }
}
