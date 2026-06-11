using Db2Simulator.Config;
using IBM.Data.Db2;

namespace Db2Simulator.Tests;

/// <summary>
/// Opens a DB2 connection for a test. Real DB2 targets use config.json connection
/// details directly; simulator targets start an embedded simulator with per-test mappings.
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

    public static Db2TestSession Create(
        IReadOnlyList<MappingConfig>? mappings = null,
        DefaultResponseConfig? defaultResponse = null)
    {
        Db2TestConnection.EnsureDriverConfigured();

        try
        {
            SimulatorConfig baseline = SimulatorConfig.Load(ConfigPath.Resolve());
            if (baseline.Auth.Users.Count == 0)
                return new Db2TestSession(baseline, "", null, "config.json auth.users is empty");

            if (IsSimulatorTarget(baseline))
            {
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

            string externalCs = Db2TestConnection.BuildConnectionString(baseline);
            string? skipReason = Db2TestConnection.Probe(externalCs);
            return new Db2TestSession(baseline, externalCs, null, skipReason);
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
                : Db2TestConnection.BuildConnectionString(_config, passwordOverride: passwordOverride);
        var conn = new DB2Connection(cs);
        conn.Open();
        return conn;
    }

    public void Dispose() => _embeddedHost?.Dispose();

    private static bool IsSimulatorTarget(SimulatorConfig config) =>
        string.Equals(config.Server.ServerName, "DB2SIM", StringComparison.OrdinalIgnoreCase);

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
