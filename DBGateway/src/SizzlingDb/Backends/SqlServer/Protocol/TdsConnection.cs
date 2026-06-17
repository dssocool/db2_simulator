using System.Net.Sockets;
using SizzlingDb.Backends.SqlServer.Sql;
using SizzlingDb.Config;
using SizzlingDb.Core.Mapping;

namespace SizzlingDb.Backends.SqlServer.Protocol;

/// <summary>Handles one client TCP connection: TDS pre-login, login, and SQL batch.</summary>
internal sealed class TdsConnection
{
    private readonly SizzlingDbConfig _config;
    private readonly StatementMapper _mapper;
    private readonly SqlServerForwarder? _forwarder;
    private readonly ResultEncoder _encoder;
    private readonly TraceLogger _log;
    private readonly TdsReader _reader;
    private readonly TdsWriter _writer;

    private bool _authenticated;
    private string _database = "";

    public TdsConnection(Stream stream, SizzlingDbConfig config, TraceLogger log)
    {
        _config = config;
        _mapper = new StatementMapper(config);
        SqlServerForwardConfig? forward = config.RequireSqlServer().Forward;
        _forwarder = forward is { IsConfigured: true } ? new SqlServerForwarder(forward) : null;
        _encoder = new ResultEncoder(config.RequireSqlServer());
        _log = log;
        _reader = new TdsReader(stream);
        _writer = new TdsWriter(stream);
        _database = config.RequireSqlServer().Database;
    }

    public void Run()
    {
        while (_reader.ReadPacket())
        {
            _log.Dump("RECV", ConcatHeader(_reader.LastType, _reader.LastStatus, _reader.LastPayload));

            switch (_reader.LastType)
            {
                case TdsTypes.PacketPreLogin:
                    HandlePreLogin();
                    break;
                case TdsTypes.PacketLogin:
                    HandleLogin();
                    break;
                case TdsTypes.PacketSqlBatch:
                case TdsTypes.PacketAttention:
                    HandleSqlBatch();
                    break;
                default:
                    _log.Info($"unhandled packet type 0x{_reader.LastType:X2}");
                    break;
            }
        }

        _log.Info("client closed the connection");
    }

    private void HandlePreLogin()
    {
        _log.Command("recv PRELOGIN");
        var clientTokens = TdsReader.ParsePreLogin(_reader.LastPayload);
        WritePreLoginResponse(clientTokens);
    }

    private void WritePreLoginResponse(Dictionary<byte, byte[]> clientTokens)
    {
        byte[] version = clientTokens.TryGetValue(TdsTypes.PreLoginVersion, out byte[]? v) && v.Length >= 4
            ? v
            : [0x10, 0x00, 0x03, 0xE8, 0x00, 0x00];

        var entries = new List<(byte Token, byte[] Value)>
        {
            (TdsTypes.PreLoginVersion, version),
            (TdsTypes.PreLoginEncryption, [TdsTypes.EncryptNotSup]),
            (TdsTypes.PreLoginInstOpt, clientTokens.GetValueOrDefault(TdsTypes.PreLoginInstOpt, [0x00])),
            (TdsTypes.PreLoginThreadId, []),
            (TdsTypes.PreLoginMars, clientTokens.GetValueOrDefault(TdsTypes.PreLoginMars, [0x00])),
            (TdsTypes.PreLoginTraceId, []),
            (TdsTypes.PreLoginFedAuth, [0x00]),
        };

        using var body = new MemoryStream();
        int pos = entries.Count * 5 + 1;
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

        byte[] payload = body.ToArray();
        _writer.WritePacket(TdsTypes.PacketResponse, TdsTypes.StatusEom, payload);
        _log.Dump("SEND", ConcatPacket(TdsTypes.PacketResponse, payload));
        _log.Command("sent PRELOGIN response (encryption=not_sup)");
    }

    private void HandleLogin()
    {
        _log.Command("recv LOGIN7");
        Login7Data login = TdsReader.ParseLogin7(_reader.LastPayload);

        if (!string.IsNullOrEmpty(login.Database))
            _database = login.Database;

        bool ok = false;
        foreach ((string user, string password) in login.CredentialCandidates())
        {
            if (_config.Auth.Validate(user, password))
            {
                ok = true;
                _log.Command($"login accepted for user '{user}' database '{_database}'");
                break;
            }
        }

        if (!ok)
        {
            _log.Command($"login rejected for user '{login.User}'");
            SendResponse(_encoder.BuildLoginError("Login failed for user."));
            return;
        }

        _authenticated = true;
        SendResponse(_encoder.BuildLoginResponse(_database, login.FeatureExt));
    }

    private void HandleSqlBatch()
    {
        if (!_authenticated)
        {
            _log.Command("rejecting SQL batch: not authenticated");
            SendResponse(_encoder.BuildError(18456, "Login failed for user."));
            return;
        }

        string sql = TdsReader.ParseSqlBatch(_reader.LastPayload);
        _log.Command($"recv SQL batch: {sql.Trim()}");

        StatementResponse response;
        if (_mapper.TryResolveMapping(sql, out StatementResponse? mapped))
            response = mapped;
        else if (_forwarder is not null)
        {
            _log.Command("forwarding SQL to upstream SQL Server");
            try
            {
                response = _forwarder.Execute(sql, _database);
            }
            catch (Exception ex)
            {
                _log.Command($"forward failed: {ex}");
                response = new ErrorResponse(50000, "42000", ex.Message);
            }
        }
        else
            response = _mapper.ResolveDefault(sql);
        byte[] payload = response switch
        {
            ResultSetResponse rs => _encoder.BuildResultSet(rs),
            UpdateCountResponse uc => _encoder.BuildUpdateCount(uc.Count),
            ErrorResponse err => _encoder.BuildError(err.SqlCode, err.Message, err.SqlState),
            _ => _encoder.BuildUpdateCount(0),
        };
        SendResponse(payload);
    }

    private void SendResponse(byte[] payload)
    {
        _writer.WritePacket(TdsTypes.PacketResponse, TdsTypes.StatusEom, payload);
        _log.Dump("SEND", ConcatPacket(TdsTypes.PacketResponse, payload));
    }

    private static byte[] ConcatHeader(byte type, byte status, ReadOnlySpan<byte> payload)
    {
        var buf = new byte[8 + payload.Length];
        buf[0] = type;
        buf[1] = status;
        buf[2] = (byte)((8 + payload.Length) >> 8);
        buf[3] = (byte)((8 + payload.Length) & 0xFF);
        payload.CopyTo(buf.AsSpan(8));
        return buf;
    }

    private static byte[] ConcatPacket(byte type, ReadOnlySpan<byte> payload)
    {
        var buf = new byte[8 + payload.Length];
        buf[0] = type;
        buf[1] = TdsTypes.StatusEom;
        buf[2] = (byte)((8 + payload.Length) >> 8);
        buf[3] = (byte)((8 + payload.Length) & 0xFF);
        payload.CopyTo(buf.AsSpan(8));
        return buf;
    }
}
