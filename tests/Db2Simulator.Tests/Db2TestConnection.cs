using Db2Simulator.Config;
using IBM.Data.Db2;

namespace Db2Simulator.Tests;

internal static class Db2TestConnection
{
    private static int _driverConfigured;

    static Db2TestConnection() => EnsureDriverConfigured();

    public static void EnsureDriverConfigured()
    {
        if (Interlocked.Exchange(ref _driverConfigured, 1) == 1)
            return;

        string clidriver = Path.Combine(AppContext.BaseDirectory, "clidriver");
        if (!Directory.Exists(clidriver))
            return;

        string lib = Path.Combine(clidriver, "lib");
        string bin = Path.Combine(clidriver, "bin");
        string icc = Path.Combine(lib, "icc");

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            string existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
            var paths = new[] { lib, icc, existing }.Where(p => !string.IsNullOrEmpty(p));
            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", string.Join(':', paths));
        }

        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!path.Split(':').Contains(bin))
            Environment.SetEnvironmentVariable("PATH", $"{bin}:{path}");
    }

    public static string BuildConnectionString(
        SimulatorConfig config,
        string? passwordOverride = null,
        string? host = null,
        int? port = null)
    {
        UserConfig user = config.Auth.Users[0];
        string resolvedHost = host ?? (config.Server.Host is "0.0.0.0" or "*"
            ? "127.0.0.1"
            : config.Server.Host);
        int resolvedPort = port ?? config.Server.Port;

        return BuildConnectionString(
            resolvedHost,
            resolvedPort,
            config.Server.Database,
            user.User,
            passwordOverride ?? user.Password);
    }

    public static string BuildConnectionString(
        DatabaseConnectionConfig config,
        string? passwordOverride = null)
    {
        return BuildConnectionString(
            config.Host,
            config.Port,
            config.Database,
            config.User,
            passwordOverride ?? config.Password);
    }

    private static string BuildConnectionString(
        string host,
        int port,
        string database,
        string user,
        string password) =>
        $"Server={host}:{port};Database={database};UID={user};PWD={password};";

    public static string? Probe(string connectionString)
    {
        try
        {
            using var conn = new DB2Connection(connectionString);
            conn.Open();
            return null;
        }
        catch (DllNotFoundException ex)
        {
            return $"DB2 driver not available: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"DB2 not reachable: {ex.Message}";
        }
    }
}
