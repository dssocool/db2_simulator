using System.Runtime.InteropServices;
using SizzlingDb.Config;
using IBM.Data.Db2;

namespace SizzlingDb.Tests;

/// <summary>Opens connections to the real DB2 configured in tests/config.json.</summary>
internal static class Db2TestConnection
{
    private static int _driverConfigured;

    public static DB2Connection Open(DatabaseConnectionConfig config)
    {
        EnsureDriverConfigured();
        var conn = new DB2Connection(BuildConnectionString(config));
        conn.Open();
        return conn;
    }

    public static string BuildConnectionString(DatabaseConnectionConfig config) =>
        $"Server={config.Host}:{config.Port};Database={config.Database};UID={config.User};PWD={config.Password};";

    /// <summary>
    /// Points the IBM driver at the clidriver bundled with the test output.
    /// Resolves the native db2 libraries from clidriver/lib; LD_LIBRARY_PATH cannot
    /// be used because the dynamic loader only reads it at process startup.
    /// </summary>
    public static void EnsureDriverConfigured()
    {
        if (Interlocked.Exchange(ref _driverConfigured, 1) == 1)
            return;

        string clidriver = Path.Combine(AppContext.BaseDirectory, "clidriver");
        if (!Directory.Exists(clidriver))
            return;

        string lib = Path.Combine(clidriver, "lib");
        string bin = Path.Combine(clidriver, "bin");

        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!path.Split(':').Contains(bin))
            Environment.SetEnvironmentVariable("PATH", $"{bin}:{path}");

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        NativeLibrary.SetDllImportResolver(typeof(DB2Connection).Assembly, (name, _, _) =>
        {
            foreach (string candidate in CandidateFileNames(name))
            {
                string full = Path.Combine(lib, candidate);
                if (File.Exists(full) && NativeLibrary.TryLoad(full, out IntPtr handle))
                    return handle;
            }

            return IntPtr.Zero;
        });
    }

    private static IEnumerable<string> CandidateFileNames(string libraryName)
    {
        yield return libraryName;
        string suffix = OperatingSystem.IsMacOS() ? ".dylib" : ".so";
        if (!libraryName.StartsWith("lib", StringComparison.Ordinal))
            yield return $"lib{libraryName}{suffix}";
        if (!libraryName.EndsWith(suffix, StringComparison.Ordinal))
            yield return $"{libraryName}{suffix}";
    }
}
