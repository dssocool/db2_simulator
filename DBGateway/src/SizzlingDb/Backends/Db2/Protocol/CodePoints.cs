namespace SizzlingDb.Backends.Db2.Protocol;

/// <summary>
/// DRDA / DDM code point constants. Values come from the DRDA V3 specification
/// (Open Group) and Apache Derby's CodePoint.java.
/// </summary>
internal static class CodePoints
{
    // ---- DSS format (low nibble of the 2nd DSS header byte) ----
    public const int DSSFMT_RQSDSS = 0x01; // request
    public const int DSSFMT_RPYDSS = 0x02; // reply
    public const int DSSFMT_OBJDSS = 0x03; // object/data
    public const byte DSS_MAGIC = 0xD0;
    public const int DSS_CHAINED = 0x40;
    public const int DSS_CONTINUE_ON_ERROR = 0x20;
    public const int DSS_SAME_ID = 0x10;
    public const int CONTINUATION_BIT = 0x8000;

    // ---- Commands ----
    public const int EXCSAT = 0x1041;
    public const int ACCSEC = 0x106D;
    public const int SECCHK = 0x106E;
    public const int ACCRDB = 0x2001;
    public const int BGNBND = 0x2002;
    public const int BNDSQLSTT = 0x2004;
    public const int CLSQRY = 0x2005;
    public const int CNTQRY = 0x2006;
    public const int DSCSQLSTT = 0x2008;
    public const int ENDBND = 0x2009;
    public const int EXCSQLIMM = 0x200A;
    public const int EXCSQLSTT = 0x200B;
    public const int OPNQRY = 0x200C;
    public const int PRPSQLSTT = 0x200D;
    public const int RDBCMM = 0x200E;
    public const int RDBRLLBCK = 0x200F;
    public const int EXCSQLSET = 0x2014;

    // ---- Command objects ----
    public const int SQLDTA = 0x2412;
    public const int SQLDTARD = 0x2413;
    public const int SQLSTT = 0x2414;
    public const int SQLATTR = 0x2450;
    public const int QRYDSC = 0x241A;
    public const int QRYDTA = 0x241B;
    public const int EXTDTA = 0x146C;
    public const int FDODSC = 0x0010;
    public const int FDODTA = 0x147A;

    // ---- Reply messages / data ----
    public const int EXCSATRD = 0x1443;
    public const int ACCSECRD = 0x14AC;
    public const int SECCHKRM = 0x1219;
    public const int ACCRDBRM = 0x2201;
    public const int ENDUOWRM = 0x220C;
    public const int RDBUPDRM = 0x2218;
    public const int OPNQRYRM = 0x2205;
    public const int ENDQRYRM = 0x220B;
    public const int OPNQFLRM = 0x2212;
    public const int RDBNFNRM = 0x2211;
    public const int RDBNACRM = 0x2204;
    public const int RDBATHRM = 0x22CB;
    public const int SQLERRRM = 0x2213;
    public const int CMDCHKRM = 0x1254;
    public const int SQLCARD = 0x2408;
    public const int SQLDARD = 0x2411;

    // ---- Parameters ----
    public const int EXTNAM = 0x115E;
    public const int SRVCLSNM = 0x1147;
    public const int SRVNAM = 0x116D;
    public const int SRVRLSLV = 0x115A;
    public const int MGRLVLLS = 0x1404;
    public const int SECMEC = 0x11A2;
    public const int SECMGRNM = 0x1196;
    public const int SECCHKCD = 0x11A4;
    public const int SECTKN = 0x11DC;
    public const int SVRCOD = 0x1149;
    public const int RDBNAM = 0x2110;
    public const int USRID = 0x11A0;
    public const int PASSWORD = 0x11A1;
    public const int NEWPASSWORD = 0x11DE;
    public const int RDBACCCL = 0x210F;
    public const int CRRTKN = 0x2135;
    public const int PRDID = 0x112E;
    public const int TYPDEFNAM = 0x002F;
    public const int TYPDEFOVR = 0x0035;
    public const int CCSIDSBC = 0x119C;
    public const int CCSIDDBC = 0x119D;
    public const int CCSIDMBC = 0x119E;
    public const int PKGNAMCSN = 0x2113;
    public const int PKGNAMCT = 0x2112;
    public const int RTNSQLDA = 0x2116;
    public const int TYPSQLDA = 0x2146;
    public const int RTNEXTDTA = 0x2148;
    public const int RDBCMTOK = 0x2105;
    public const int QRYBLKSZ = 0x2114;
    public const int QRYBLKCTL = 0x2132;
    public const int MAXBLKEXT = 0x2141;
    public const int QRYCLSIMP = 0x215D;
    public const int QRYINSID = 0x215B;
    public const int QRYPRCTYP = 0x2102;
    public const int QRYROWSET = 0x2156;
    public const int SQLCSRHLD = 0x211F;
    public const int FIXROWPRC = 0x2418;
    public const int LMTBLKPRC = 0x2417;

    // ---- Manager code points ----
    public const int AGENT = 0x1403;
    public const int SQLAM = 0x2407;
    public const int RDB = 0x240F;
    public const int SECMGR = 0x1440;
    public const int CMNTCPIP = 0x1474;
    public const int CCSIDMGR = 0x14CC;
    public const int UNICODEMGR = 0x1C08;
    public const int XAMGR = 0x1C01;
    public const int SYNCPTMGR = 0x14C0;
    public const int RSYNCMGR = 0x14C1;
    public const int SUPERVISOR = 0x143C;

    // ---- Enumerated values ----
    public const int SQLAM_LEVEL = 7;     // SQLAM manager level we operate at
    public const int SECMEC_USRIDPWD = 3;
    public const int SECMEC_USRIDONL = 4;
    public const int SECMEC_USRENCPWD = 7;
    public const int SECMEC_EUSRIDPWD = 9;

    public const int SECCHKCD_OK = 0x00;
    public const int SECCHKCD_NOTSUPPORTED = 0x01;
    public const int SECCHKCD_SECTKNMISSING = 0x0E;
    public const int SECCHKCD_PASSWORD_INVALID = 0x0F;
    public const int SECCHKCD_PASSWORD_MISSING = 0x10;
    public const int SECCHKCD_USERID_MISSING = 0x12;
    public const int SECCHKCD_USERID_INVALID = 0x13;

    public const int SVRCOD_INFO = 0;
    public const int SVRCOD_WARNING = 4;
    public const int SVRCOD_ERROR = 8;

    public const int TYPSQLDA_STD_OUTPUT = 0;
    public const int TYPSQLDA_LIGHT_OUTPUT = 2;

    public const int NULLDATA = 0xFF;

    public static string Name(int codePoint) => codePoint switch
    {
        EXCSAT => "EXCSAT",
        ACCSEC => "ACCSEC",
        SECCHK => "SECCHK",
        ACCRDB => "ACCRDB",
        BGNBND => "BGNBND",
        BNDSQLSTT => "BNDSQLSTT",
        CLSQRY => "CLSQRY",
        CNTQRY => "CNTQRY",
        DSCSQLSTT => "DSCSQLSTT",
        ENDBND => "ENDBND",
        EXCSQLIMM => "EXCSQLIMM",
        EXCSQLSTT => "EXCSQLSTT",
        OPNQRY => "OPNQRY",
        PRPSQLSTT => "PRPSQLSTT",
        RDBCMM => "RDBCMM",
        RDBRLLBCK => "RDBRLLBCK",
        EXCSQLSET => "EXCSQLSET",
        SQLSTT => "SQLSTT",
        SQLATTR => "SQLATTR",
        SQLDTA => "SQLDTA",
        EXTDTA => "EXTDTA",
        _ => "0x" + codePoint.ToString("X4"),
    };
}
