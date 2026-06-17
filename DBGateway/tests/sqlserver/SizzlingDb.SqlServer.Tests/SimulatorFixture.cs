using SizzlingDb.Backends.SqlServer;
using SizzlingDb.Config;
using SizzlingDb.Core;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>Starts the SQL Server TDS simulator on a dynamic port for the test run.</summary>
public sealed class SimulatorFixture : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _serverThread;
    public SizzlingDbConfig Config { get; }
    public int Port { get; }

    public SimulatorFixture()
    {
        Port = GetFreePort();
        Config = BuildConfig(Port);
        ISimulatorBackend backend = new SqlServerSimulatorBackend(Config);
        _serverThread = new Thread(() => backend.Run(_cts.Token)) { IsBackground = true };
        _serverThread.Start();
        Thread.Sleep(200);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serverThread.Join(TimeSpan.FromSeconds(3));
        _cts.Dispose();
    }

    private static SizzlingDbConfig BuildConfig(int port)
    {
        string defaultDataPath = ResolveDefaultDataPath();
        MappingData data = MappingData.Load(defaultDataPath);

        return new SizzlingDbConfig
        {
            Database = new DatabaseConfig { Type = "sqlserver" },
            Backends = new BackendsConfig
            {
                SqlServer = new SqlServerBackendConfig
                {
                    Host = "127.0.0.1",
                    Port = port,
                    Database = "master",
                    ServerName = "SIZZLINGDB",
                },
            },
            Auth = new AuthConfig
            {
                RequirePassword = true,
                CaseInsensitiveUser = true,
                Users =
                [
                    new UserConfig { User = "dev_user", Password = "YourStrongPassword123" },
                ],
            },
            Trace = new TraceConfig { LogCommands = false, HexDump = false },
            Matching = new MatchingConfig
            {
                IgnoreCase = true,
                CollapseWhitespace = true,
                TrimTrailingSemicolon = true,
            },
            DefaultResponse = data.DefaultResponse,
            Mappings = data.Mappings,
        };
    }

    private static string ResolveDefaultDataPath()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "config", "backends", "sqlserver", "default_data.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "backends", "sqlserver", "default_data.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "backends", "sqlserver", "default_data.json"),
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
public sealed class SimulatorCollection : ICollectionFixture<SimulatorFixture>
{
    public const string Name = "SqlServerSimulator";
}
