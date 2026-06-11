using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Db2Simulator.Config;
using Db2Simulator.Sql;

namespace Db2Simulator.Protocol;

/// <summary>Handles one client TCP connection: the DRDA handshake and SQL flow.</summary>
internal sealed class DrdaConnection
{
    private readonly SimulatorConfig _config;
    private readonly StatementMapper _mapper;
    private readonly ResultEncoder _encoder;
    private readonly TraceLogger _log;
    private readonly bool _littleData;

    private readonly DssReader _reader;
    private readonly DssWriter _writer;

    private bool _authenticated;
    private int _securityMechanism;
    private string _user = "";
    private int _clientCcsidSbc;
    private int _clientCcsidMbc;
    private bool _unicodeNegotiated;
    private string _currentSql = "";
    private StatementResponse? _prepared;
    private bool _useCompactSqldard;

    public DrdaConnection(Stream stream, SimulatorConfig config, TraceLogger log)
    {
        _config = config;
        _mapper = new StatementMapper(config);
        _littleData = config.Server.LittleEndianData;
        _encoder = new ResultEncoder(_littleData, config.Server.ProductId);
        _log = log;
        _reader = new DssReader(stream);
        _writer = new DssWriter(stream);
    }

    public void Run()
    {
        while (true)
        {
            List<DssFrame>? chain = _reader.ReadChain();
            if (chain is null)
            {
                _log.Info("client closed the connection");
                return;
            }

            _log.Dump("RECV", _reader.LastChainRaw);

            List<CommandUnit> units = GroupUnits(chain);
            foreach (CommandUnit unit in units)
                Process(unit);

            if (_writer.PendingCount > 0)
            {
                byte[] sent = _writer.Flush();
                _log.Dump("SEND", sent);
            }
        }
    }

    // ---- group object DSS (SQLSTT/SQLDTA/...) with their preceding command ----

    private sealed class CommandUnit
    {
        public required int CorrId;
        public required int CodePoint;
        public required byte[] Payload;
        public List<DssFrame> Objects { get; } = new();
    }

    private static bool IsObjectCodePoint(int cp) =>
        cp is CodePoints.SQLSTT or CodePoints.SQLATTR or CodePoints.SQLDTA or CodePoints.EXTDTA;

    private List<CommandUnit> GroupUnits(List<DssFrame> chain)
    {
        var units = new List<CommandUnit>();
        CommandUnit? current = null;
        foreach (DssFrame f in chain)
        {
            if (f.IsObject || IsObjectCodePoint(f.CodePoint))
            {
                if (current is not null)
                    current.Objects.Add(f);
                continue;
            }
            current = new CommandUnit { CorrId = f.CorrelationId, CodePoint = f.CodePoint, Payload = f.Payload };
            units.Add(current);
        }
        return units;
    }

    private void Process(CommandUnit unit)
    {
        _log.Command($"recv {CodePoints.Name(unit.CodePoint)} (corr={unit.CorrId}, objs={unit.Objects.Count})");

        if (!_authenticated && RequiresAuthentication(unit.CodePoint))
        {
            RejectUnauthenticated(unit);
            return;
        }

        switch (unit.CodePoint)
        {
            case CodePoints.EXCSAT: HandleExcsat(unit); break;
            case CodePoints.ACCSEC: HandleAccsec(unit); break;
            case CodePoints.SECCHK: HandleSecchk(unit); break;
            case CodePoints.ACCRDB: HandleAccrdb(unit); break;
            case CodePoints.EXCSQLSET: ReplyObject(unit, _encoder.BuildSqlcardSuccess()); break;
            case CodePoints.PRPSQLSTT: HandlePrepare(unit); break;
            case CodePoints.DSCSQLSTT: HandleDescribe(unit); break;
            case CodePoints.OPNQRY: HandleOpenQuery(unit); break;
            case CodePoints.EXCSQLSTT: HandleExecute(unit); break;
            case CodePoints.EXCSQLIMM: HandleExecuteImmediate(unit); break;
            case CodePoints.CNTQRY: HandleContinueQuery(unit); break;
            case CodePoints.CLSQRY: Reply(unit, _encoder.BuildSqlcardSuccess()); break;
            case CodePoints.RDBCMM: HandleCommit(unit); break;
            case CodePoints.RDBRLLBCK: HandleCommit(unit); break;
            case CodePoints.BGNBND:
            case CodePoints.BNDSQLSTT:
            case CodePoints.ENDBND:
                ReplyObject(unit, _encoder.BuildSqlcardSuccess());
                break;
            default:
                _log.Info($"unhandled command {CodePoints.Name(unit.CodePoint)}; replying success");
                ReplyObject(unit, _encoder.BuildSqlcardSuccess());
                break;
        }
    }

    private static bool RequiresAuthentication(int codePoint) => codePoint switch
    {
        CodePoints.EXCSAT or CodePoints.ACCSEC or CodePoints.SECCHK => false,
        _ => true,
    };

    private void RejectUnauthenticated(CommandUnit unit)
    {
        _log.Command($"rejecting {CodePoints.Name(unit.CodePoint)}: not authenticated");
        if (unit.CodePoint == CodePoints.ACCRDB)
        {
            var d = new Ddm(_littleData).Begin(CodePoints.RDBATHRM);
            d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_ERROR);
            d.ScalarString(CodePoints.RDBNAM, PadRdbnam(_config.Server.Database));
            Reply(unit, d.End().ToArray());
        }
        else
        {
            ReplyObject(unit, _encoder.BuildSqlcardError(-1542, "42505",
                "DB2SIM: connection is not authenticated"));
        }
    }

    private static string PadRdbnam(string name) => name.Length >= 18 ? name : name.PadRight(18);

    // ---------------- handshake ----------------

    private void HandleExcsat(CommandUnit unit)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.EXCSATRD);
        d.ScalarString(CodePoints.EXTNAM, _config.Server.ServerName);

        foreach ((int cp, byte[] value) in DssReader.ParseParameters(unit.Payload))
        {
            if (cp != CodePoints.MGRLVLLS)
                continue;
            d.Begin(CodePoints.MGRLVLLS);
            for (int i = 0; i + 4 <= value.Length; i += 4)
            {
                int manager = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(i, 2));
                int level = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(i + 2, 2));
                if (manager == CodePoints.UNICODEMGR)
                    _unicodeNegotiated = true;
                if (manager == CodePoints.SQLAM)
                    level = Math.Min(level, CodePoints.SQLAM_LEVEL);
                d.WriteU16BE(manager).WriteU16BE(level);
            }
            d.End();
        }

        d.ScalarString(CodePoints.SRVCLSNM, _config.Server.ServerClassName);
        d.ScalarString(CodePoints.SRVNAM, _config.Server.ServerName);
        d.ScalarString(CodePoints.SRVRLSLV, _config.Server.ProductId);
        Reply(unit, d.End().ToArray());
    }

    private void HandleAccsec(CommandUnit unit)
    {
        int mechanism = CodePoints.SECMEC_USRIDPWD;
        foreach ((int cp, byte[] value) in DssReader.ParseParameters(unit.Payload))
        {
            if (cp == CodePoints.SECMEC && value.Length >= 2)
                mechanism = BinaryPrimitives.ReadUInt16BigEndian(value);
        }

        bool supported = mechanism is CodePoints.SECMEC_USRIDPWD or CodePoints.SECMEC_USRIDONL;
        var d = new Ddm(_littleData).Begin(CodePoints.ACCSECRD);
        if (supported)
        {
            _securityMechanism = mechanism;
            d.Scalar2(CodePoints.SECMEC, mechanism);
            _log.Command($"ACCSEC secmec={mechanism} accepted");
        }
        else
        {
            // Renegotiate: tell the client to use plain user/password.
            d.Scalar2(CodePoints.SECMEC, CodePoints.SECMEC_USRIDPWD);
            _log.Command($"ACCSEC secmec={mechanism} unsupported; offering USRIDPWD");
        }
        Reply(unit, d.End().ToArray());
    }

    private void HandleSecchk(CommandUnit unit)
    {
        string user = "";
        string password = "";
        bool sawPassword = false;
        foreach ((int cp, byte[] value) in DssReader.ParseParameters(unit.Payload))
        {
            switch (cp)
            {
                case CodePoints.USRID: user = Ccsid.Ebcdic.GetString(value).Trim(); break;
                case CodePoints.PASSWORD: password = Ccsid.Ebcdic.GetString(value).Trim(); sawPassword = true; break;
            }
        }

        _user = user;
        int code;
        if (_securityMechanism == CodePoints.SECMEC_USRIDONL)
        {
            code = _config.Auth.UserExists(user) || !_config.Auth.RequirePassword
                ? CodePoints.SECCHKCD_OK
                : CodePoints.SECCHKCD_USERID_INVALID;
        }
        else if (_config.Auth.RequirePassword && !sawPassword)
        {
            code = CodePoints.SECCHKCD_PASSWORD_MISSING;
        }
        else if (_config.Auth.Validate(user, password))
        {
            code = CodePoints.SECCHKCD_OK;
        }
        else
        {
            code = _config.Auth.UserExists(user)
                ? CodePoints.SECCHKCD_PASSWORD_INVALID
                : CodePoints.SECCHKCD_USERID_INVALID;
        }

        _authenticated = code == CodePoints.SECCHKCD_OK;
        int svrcod = _authenticated ? CodePoints.SVRCOD_INFO : CodePoints.SVRCOD_ERROR;
        _log.Command($"SECCHK user='{user}' -> {(_authenticated ? "OK" : "REJECT")} (code=0x{code:X2})");

        var d = new Ddm(_littleData).Begin(CodePoints.SECCHKRM);
        d.Scalar2(CodePoints.SVRCOD, svrcod);
        d.Scalar1(CodePoints.SECCHKCD, code);
        Reply(unit, d.End().ToArray());
    }

    private void HandleAccrdb(CommandUnit unit)
    {
        foreach ((int cp, byte[] value) in DssReader.ParseParameters(unit.Payload))
        {
            if (LooksLikeOleDbClient(cp, value))
                _useCompactSqldard = true;

            if (cp != CodePoints.TYPDEFOVR)
                continue;
            foreach ((int icp, byte[] ivalue) in DssReader.ParseParameters(value))
            {
                int ccsid = ivalue.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(ivalue) : 0;
                if (icp == CodePoints.CCSIDSBC) _clientCcsidSbc = ccsid;
                else if (icp == CodePoints.CCSIDMBC) _clientCcsidMbc = ccsid;
            }
        }

        var d = new Ddm(_littleData).Begin(CodePoints.ACCRDBRM);
        d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_INFO);
        d.ScalarString(CodePoints.PRDID, _config.Server.ProductId);
        d.ScalarString(CodePoints.TYPDEFNAM, _config.Server.TypeDefName);
        d.Begin(CodePoints.TYPDEFOVR);
        d.Scalar2(CodePoints.CCSIDSBC, Ccsid.Utf8);
        d.Scalar2(CodePoints.CCSIDDBC, Ccsid.Utf16Be);
        d.Scalar2(CodePoints.CCSIDMBC, Ccsid.Utf8);
        d.End();
        Reply(unit, d.End().ToArray());
        _log.Command($"ACCRDB database accessed (PRDID={_config.Server.ProductId}, {_config.Server.TypeDefName}, compactSQLDARD={_useCompactSqldard})");
    }

    private static bool LooksLikeOleDbClient(int codePoint, byte[] value)
    {
        if (value.Length == 0)
            return false;

        string text = Encoding.ASCII.GetString(value);
        return text.Contains("qlservr", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MSOLEDBSQL", StringComparison.OrdinalIgnoreCase)
            || text.Contains("MSDRDA", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- SQL ----------------

    private void HandlePrepare(CommandUnit unit)
    {
        bool returnSqlda = HasFlag(unit.Payload, CodePoints.RTNSQLDA);
        _currentSql = ReadStatementText(unit) ?? _currentSql;
        _prepared = _mapper.Resolve(_currentSql);
        _log.Command($"PREPARE: {_currentSql} (RTNSQLDA={returnSqlda})");

        switch (_prepared)
        {
            case ErrorResponse e:
                _log.Command($"PREPARE -> SQLCARD error {e.SqlCode}/{e.SqlState}");
                ReplyObject(unit, _encoder.BuildSqlcardError(e.SqlCode, e.SqlState, e.Message));
                break;
            case ResultSetResponse rs when returnSqlda:
                _log.Command($"PREPARE -> SQLDARD ({rs.Columns.Count} cols)");
                ReplyObject(unit, _encoder.BuildSqldard(rs.Columns, _config.Server.Database, _useCompactSqldard));
                break;
            default:
                _log.Command("PREPARE -> SQLCARD success");
                ReplyObject(unit, _encoder.BuildSqlcardSuccess());
                break;
        }
    }

    private void HandleDescribe(CommandUnit unit)
    {
        StatementResponse resp = _prepared ?? _mapper.Resolve(_currentSql);
        switch (resp)
        {
            case ErrorResponse e:
                ReplyObject(unit, _encoder.BuildSqlcardError(e.SqlCode, e.SqlState, e.Message));
                break;
            case ResultSetResponse rs:
                ReplyObject(unit, _encoder.BuildSqldard(rs.Columns, _config.Server.Database, _useCompactSqldard));
                break;
            default:
                ReplyObject(unit, _encoder.BuildSqlcardSuccess());
                break;
        }
    }

    private void HandleOpenQuery(CommandUnit unit)
    {
        StatementResponse resp = _prepared ?? _mapper.Resolve(_currentSql);
        if (resp is ResultSetResponse rs)
        {
            _log.Command($"OPNQRY: {rs.Columns.Count} cols, {rs.Rows.Count} rows");
            Reply(unit, BuildOpnqryrm());
            ReplyObject(unit, _encoder.BuildQrydsc(rs.Columns, _useCompactSqldard));
            ReplyObject(unit, _encoder.BuildQrydta(rs.Columns, rs.Rows));
            Reply(unit, BuildEndqryrm());
            ReplyObject(unit, _encoder.BuildSqlcardEndOfData(rs.Rows.Count, _config.Server.Database));
        }
        else if (resp is ErrorResponse e)
        {
            Reply(unit, BuildOpnqflrm());
            ReplyObject(unit, _encoder.BuildSqlcardError(e.SqlCode, e.SqlState, e.Message));
        }
        else
        {
            // No result set to open; surface as an error.
            Reply(unit, BuildOpnqflrm());
            ReplyObject(unit, _encoder.BuildSqlcardError(-204, "42704",
                "DB2SIM: statement does not produce a result set"));
        }
    }

    private void HandleExecute(CommandUnit unit)
    {
        StatementResponse resp = _prepared ?? _mapper.Resolve(_currentSql);
        ReplyExecuteResult(unit, resp);
    }

    private void HandleExecuteImmediate(CommandUnit unit)
    {
        _currentSql = ReadStatementText(unit) ?? _currentSql;
        StatementResponse resp = _mapper.Resolve(_currentSql);
        _log.Command($"EXECUTE IMMEDIATE: {_currentSql}");
        ReplyExecuteResult(unit, resp);
    }

    private void ReplyExecuteResult(CommandUnit unit, StatementResponse resp)
    {
        switch (resp)
        {
            case ErrorResponse e:
                ReplyObject(unit, _encoder.BuildSqlcardError(e.SqlCode, e.SqlState, e.Message));
                break;
            case UpdateCountResponse uc:
                ReplyObject(unit, _encoder.BuildSqlcardUpdate(uc.Count));
                break;
            default:
                ReplyObject(unit, _encoder.BuildSqlcardSuccess());
                break;
        }
    }

    private void HandleContinueQuery(CommandUnit unit)
    {
        // The simulator returns the whole answer set in one block, so any CNTQRY
        // arrives after the cursor is already exhausted.
        Reply(unit, BuildEndqryrm());
        ReplyObject(unit, _encoder.BuildSqlcardSuccess());
    }

    private void HandleCommit(CommandUnit unit)
    {
        var d = new Ddm(_littleData).Begin(CodePoints.ENDUOWRM);
        d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_INFO);
        Reply(unit, d.End().ToArray());
        ReplyObject(unit, _encoder.BuildSqlcardSuccess());
    }

    // ---------------- reply message builders ----------------

    private byte[] BuildOpnqryrm()
    {
        var d = new Ddm(_littleData).Begin(CodePoints.OPNQRYRM);
        d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_INFO);
        d.Scalar2(CodePoints.QRYPRCTYP, CodePoints.LMTBLKPRC);
        var insid = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(insid.AsSpan(4), 1);
        d.ScalarBytes(CodePoints.QRYINSID, insid);
        return d.End().ToArray();
    }

    private byte[] BuildEndqryrm()
    {
        var d = new Ddm(_littleData).Begin(CodePoints.ENDQRYRM);
        d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_WARNING);
        d.ScalarString(CodePoints.RDBNAM, PadRdbnam(_config.Server.Database));
        return d.End().ToArray();
    }

    private byte[] BuildOpnqflrm()
    {
        var d = new Ddm(_littleData).Begin(CodePoints.OPNQFLRM);
        d.Scalar2(CodePoints.SVRCOD, CodePoints.SVRCOD_ERROR);
        return d.End().ToArray();
    }

    // ---------------- helpers ----------------

    private void Reply(CommandUnit unit, byte[] ddm) => _writer.AddReply(unit.CorrId, ddm);

    private void ReplyObject(CommandUnit unit, byte[] ddm) => _writer.AddObject(unit.CorrId, ddm);

    private static bool HasFlag(byte[] payload, int codePoint)
    {
        foreach ((int cp, byte[] value) in DssReader.ParseParameters(payload))
            if (cp == codePoint && value.Length >= 1 && value[0] != 0xF0 && value[0] != 0x00)
                return true;
        return false;
    }

    private string? ReadStatementText(CommandUnit unit)
    {
        foreach (DssFrame obj in unit.Objects)
        {
            if (obj.CodePoint != CodePoints.SQLSTT)
                continue;
            return DecodeSqlstt(obj.Payload);
        }
        return null;
    }

    private string? DecodeSqlstt(byte[] payload)
    {
        if (payload.Length == 0)
            return null;
        if (payload[0] == 0xFF) // null statement
            return null;

        // null indicator (0x00) + 4-byte big-endian length + bytes
        if (payload.Length < 5)
            return null;
        int len = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(1, 4));
        int available = payload.Length - 5;
        if (len > available)
            len = available;
        byte[] sqlBytes = payload[5..(5 + len)];
        return SqlEncoding().GetString(sqlBytes).TrimEnd('\0', ' ');
    }

    private System.Text.Encoding SqlEncoding()
    {
        if (_unicodeNegotiated)
            return System.Text.Encoding.UTF8;
        if (_clientCcsidMbc != 0)
            return Ccsid.ForCcsid(_clientCcsidMbc);
        if (_clientCcsidSbc != 0)
            return Ccsid.ForCcsid(_clientCcsidSbc);
        return System.Text.Encoding.UTF8;
    }
}
