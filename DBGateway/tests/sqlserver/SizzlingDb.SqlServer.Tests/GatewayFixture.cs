using SizzlingDb.Backends.SqlServer;
using SizzlingDb.Config;
using SizzlingDb.Core;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Starts the SQL Server TDS gateway on a dynamic port with forwarding enabled to
/// the real SQL Server from tests/config.json.
/// </summary>
public sealed class GatewayFixture : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _serverThread;
    public SizzlingDbConfig Config { get; }
    public int Port { get; }
    public SqlServerConnectionConfig Upstream { get; }

    public GatewayFixture()
    {
        TestConfig.EnsureLoaded();
        Upstream = TestConfig.RequireSqlServer();
        Port = GetFreePort();
        Config = BuildConfig(Port, Upstream);
        ISimulatorBackend backend = new SqlServerSimulatorBackend(Config);
        _serverThread = new Thread(() => backend.Run(_cts.Token)) { IsBackground = true };
        _serverThread.Start();
        Thread.Sleep(300);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serverThread.Join(TimeSpan.FromSeconds(3));
        _cts.Dispose();
    }

    private static SizzlingDbConfig BuildConfig(int port, SqlServerConnectionConfig upstream)
    {
        string defaultDataPath = ResolveDefaultDataPath();
        MappingData data = MappingData.Load(defaultDataPath);

        var config = new SizzlingDbConfig
        {
            GatewayMode = new GatewayModeConfig
            {
                SqlServer = new SqlServerBackendConfig
                {
                    Host = "127.0.0.1",
                    Port = port,
                    Database = upstream.Database,
                    ServerName = "SIZZLINGDB",
                    Forward = new SqlServerForwardConfig
                    {
                        Host = upstream.Host,
                        Port = upstream.Port,
                        Database = upstream.Database,
                        User = upstream.User,
                        Password = upstream.Password,
                    },
                },
            },
            Auth = new AuthConfig
            {
                RequirePassword = true,
                CaseInsensitiveUser = true,
                Users =
                [
                    new UserConfig { User = upstream.User, Password = upstream.Password },
                ],
            },
            Trace = new TraceConfig { LogCommands = true, HexDump = false },
            Matching = new MatchingConfig
            {
                IgnoreCase = true,
                CollapseWhitespace = true,
                TrimTrailingSemicolon = true,
            },
            DefaultResponse = data.DefaultResponse,
            Mappings = data.Mappings,
        };
        config.Validate();
        return config;
    }

    private static string ResolveDefaultDataPath()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "config", "backends", "sqlserver", "default_data.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DBGateway", "config", "backends", "sqlserver", "default_data.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "backends", "sqlserver", "default_data.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "backends", "sqlserver", "default_data.json"),
        ];
        foreach (string c in candidates)
        {
            string full = Path.GetFullPath(c);
            if (File.Exists(full))
                return full;
        }
        throw new FileNotFoundException("config/backends/sqlserver/default_data.json not found.");
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class GatewayCollection : ICollectionFixture<GatewayFixture>
{
    public const string Name = "SqlServerGateway";
}

/// <summary>Lazily starts the gateway so Test02_Setup can run first without a collection.</summary>
internal static class GatewayServer
{
    private static readonly Lazy<GatewayFixture> Instance = new(() => new GatewayFixture(), isThreadSafe: true);
    public static GatewayFixture Fixture => Instance.Value;
}
