using System.Text;

namespace Db2Simulator.Protocol;

/// <summary>
/// Coded Character Set Identifier helpers. DRDA protocol strings (USRID, PASSWORD,
/// RDBNAM, PRDID, server attributes) are carried in EBCDIC by default, while data
/// strings (column names, character values) are typically UTF-8 (CCSID 1208).
/// </summary>
internal static class Ccsid
{
    public const int Ebcdic037 = 37;
    public const int Ebcdic500 = 500;
    public const int Utf8 = 1208;
    public const int Utf16Be = 1200;

    private static Encoding? _ebcdic;

    static Ccsid()
    {
        // Register IBM EBCDIC and Windows code pages (needed on Linux/.NET).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>EBCDIC encoding used for the DDM protocol layer (CCSID 500, multilingual).</summary>
    public static Encoding Ebcdic => _ebcdic ??= Encoding.GetEncoding(Ebcdic500);

    /// <summary>Map a CCSID to a .NET Encoding, defaulting to UTF-8 for unknown values.</summary>
    public static Encoding ForCcsid(int ccsid) => ccsid switch
    {
        0 or Utf8 or 1209 or 65001 => Encoding.UTF8,
        Utf16Be or 1202 or 13488 or 1201 => Encoding.BigEndianUnicode,
        Ebcdic037 or 1140 => Encoding.GetEncoding(Ebcdic037),
        Ebcdic500 or 1148 => Encoding.GetEncoding(Ebcdic500),
        819 or 28591 => Encoding.Latin1,
        1252 => Encoding.GetEncoding(1252),
        _ => Encoding.UTF8,
    };
}
