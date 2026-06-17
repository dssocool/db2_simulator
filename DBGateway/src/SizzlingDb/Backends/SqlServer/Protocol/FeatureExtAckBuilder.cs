using System.Buffers.Binary;

namespace SizzlingDb.Backends.SqlServer.Protocol;

internal static class FeatureExtAckBuilder
{
    /// <summary>Builds FEATUREEXTACK bytes for the features requested in Login7.</summary>
    public static byte[] BuildAck(ReadOnlySpan<byte> clientFeatureExt)
    {
        using var body = new MemoryStream();
        int i = 0;
        while (i < clientFeatureExt.Length)
        {
            byte featureId = clientFeatureExt[i++];
            if (featureId == 0xFF)
                break;

            if (i + 4 > clientFeatureExt.Length)
                break;

            int dataLen = BinaryPrimitives.ReadInt32LittleEndian(clientFeatureExt.Slice(i, 4));
            i += 4;
            ReadOnlySpan<byte> clientData = dataLen > 0 && i + dataLen <= clientFeatureExt.Length
                ? clientFeatureExt.Slice(i, dataLen)
                : ReadOnlySpan<byte>.Empty;
            i += dataLen;

            WriteFeatureAck(body, featureId, clientData);
        }

        body.WriteByte(0xFF);
        return body.ToArray();
    }

    private static void WriteFeatureAck(Stream body, byte featureId, ReadOnlySpan<byte> clientData)
    {
        if (featureId is 0x01 or 0x05 or 0x0D)
            return;
        ReadOnlySpan<byte> ackData = featureId switch
        {
            0x01 => ReadOnlySpan<byte>.Empty,
            0x04 => Ack04,
            0x05 => ReadOnlySpan<byte>.Empty,
            0x09 => Ack09,
            0x0A => Ack0A,
            0x0B => Ack0B,
            0x0D => clientData.Length > 0 ? clientData : Ack0D,
            _ => clientData,
        };

        body.WriteByte(featureId);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, ackData.Length);
        body.Write(len);
        if (!ackData.IsEmpty)
            body.Write(ackData);
    }

    private static ReadOnlySpan<byte> Ack04 => [0x01];
    private static ReadOnlySpan<byte> Ack09 => [0x02, 0x01];
    private static ReadOnlySpan<byte> Ack0A => [0x01];
    private static ReadOnlySpan<byte> Ack0B => [0x00];
    private static ReadOnlySpan<byte> Ack0D => [0x01];
}
