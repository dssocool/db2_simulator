using Db2Simulator.Config;
using IBM.Data.Db2;

namespace Db2Simulator.Tests;

/// <summary>
/// Opens a DB2 connection for a test. Embedded-simulator tests use the top-level
/// server/auth settings; real-DB2 tests use tests.db2 from config.json.
/// </summary>
internal sealed class Db2TestSession : IDisposable
{
    private readonly SimulatorConfig _config;
    private readonly string _connectionString;
    private readonly EmbeddedSimulatorHost? _embeddedHost;

    public string? SkipReason { get; }

    private Db2TestSession(SimulatorConfig config, string connectionString, EmbeddedSimulatorHost? embeddedHost, string? skipReason)
    {
        _config = config;
        _connectionString = connectionString;
        _embeddedHost = embeddedHost;
        SkipReason = skipReason;
    }

    public static Db2TestSession CreateEmbedded(
        IReadOnlyList<MappingConfig>? mappings = null,
        DefaultResponseConfig? defaultResponse = null) =>
        Create(Db2TestTarget.EmbeddedSimulator, mappings, defaultResponse);

    public static Db2TestSession CreateReal() =>
        Create(Db2TestTarget.RealDb2);

    public static Db2TestSession Create(
        Db2TestTarget target = Db2TestTarget.EmbeddedSimulator,
        IReadOnlyList<MappingConfig>? mappings = null,
        DefaultResponseConfig? defaultResponse = null)
    {
        Db2TestConnection.EnsureDriverConfigured();

        try
        {
            SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());

            if (target == Db2TestTarget.RealDb2)
            {
                DatabaseConnectionConfig? db2 = baseline.Tests.Db2;
                if (db2 is null || !db2.IsConfigured)
                    return new Db2TestSession(baseline, "", null, "tests.db2 is not configured");

                string externalCs = Db2TestConnection.BuildConnectionString(db2);
                string? skipReason = Db2TestConnection.Probe(externalCs);
                return new Db2TestSession(baseline, externalCs, null, skipReason);
            }

            if (baseline.Auth.Users.Count == 0)
                return new Db2TestSession(baseline, "", null, "config.json auth.users is empty");

            SimulatorConfig simConfig = BuildSimulatorConfig(baseline, mappings ?? [], defaultResponse);
            var host = new EmbeddedSimulatorHost(simConfig);
            string connectionString = Db2TestConnection.BuildConnectionString(simConfig, host: "127.0.0.1", port: host.Port);
            string? embeddedSkip = Db2TestConnection.Probe(connectionString);
            if (embeddedSkip is not null)
            {
                host.Dispose();
                return new Db2TestSession(simConfig, connectionString, null, embeddedSkip);
            }

            return new Db2TestSession(simConfig, connectionString, host, null);
        }
        catch (Exception ex)
        {
            return new Db2TestSession(new SimulatorConfig(), "", null, ex.Message);
        }
    }

    public DB2Connection Open(string? passwordOverride = null)
    {
        if (SkipReason is not null)
            throw new InvalidOperationException(SkipReason);

        string cs = passwordOverride is null
            ? _connectionString
            : _embeddedHost is not null
                ? Db2TestConnection.BuildConnectionString(
                    _config, passwordOverride: passwordOverride, host: "127.0.0.1", port: _embeddedHost.Port)
                : Db2TestConnection.BuildConnectionString(_config.Tests.Db2!, passwordOverride: passwordOverride);
        var conn = new DB2Connection(cs);
        conn.Open();
        return conn;
    }

    public void Dispose() => _embeddedHost?.Dispose();

    private static SimulatorConfig BuildSimulatorConfig(
        SimulatorConfig baseline,
        IReadOnlyList<MappingConfig> mappings,
        DefaultResponseConfig? defaultResponse) =>
        new()
        {
            Server = new ServerConfig
            {
                Host = baseline.Server.Host,
                Port = baseline.Server.Port,
                Database = baseline.Server.Database,
                ServerClassName = baseline.Server.ServerClassName,
                ServerName = baseline.Server.ServerName,
                ProductId = baseline.Server.ProductId,
                DataEndian = baseline.Server.DataEndian,
            },
            Auth = baseline.Auth,
            Trace = baseline.Trace,
            Matching = baseline.Matching,
            Mappings = mappings.ToList(),
            DefaultResponse = defaultResponse,
        };
}

internal enum Db2TestTarget
{
    EmbeddedSimulator,
    RealDb2,
}
