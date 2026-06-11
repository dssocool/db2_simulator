using System.Text;

namespace Db2Simulator.Protocol;

/// <summary>Lightweight console logger with optional DSS hex dumps.</summary>
internal sealed class TraceLogger
{
    private static readonly object Gate = new();
    private readonly bool _logCommands;
    private readonly bool _hexDump;
    private readonly string _peer;

    public TraceLogger(bool logCommands, bool hexDump, string peer)
    {
        _logCommands = logCommands;
        _hexDump = hexDump;
        _peer = peer;
    }

    public void Info(string message)
    {
        lock (Gate)
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{_peer}] {message}");
    }

    public void Command(string message)
    {
        if (_logCommands)
            Info(message);
    }

    public void Dump(string label, ReadOnlySpan<byte> bytes)
    {
        if (!_hexDump)
            return;
        lock (Gate)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{_peer}] {label} ({bytes.Length} bytes)");
            Console.WriteLine(HexDump(bytes));
        }
    }

    private static string HexDump(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append("  ").Append(i.ToString("X4")).Append("  ");
            int end = Math.Min(i + 16, bytes.Length);
            for (int j = i; j < end; j++)
                sb.Append(bytes[j].ToString("X2")).Append(' ');
            for (int j = end; j < i + 16; j++)
                sb.Append("   ");
            sb.Append(' ');
            for (int j = i; j < end; j++)
            {
                byte b = bytes[j];
                sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd('\n', '\r');
    }
}
