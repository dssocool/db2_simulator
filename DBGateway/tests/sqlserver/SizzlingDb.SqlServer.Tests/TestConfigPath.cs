namespace SizzlingDb.SqlServer.Tests;

internal static class TestConfigPath
{
    public static string Resolve()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "sqlserver", "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DBGateway", "tests", "sqlserver", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tests", "sqlserver", "config.json"),
        ];

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        throw new FileNotFoundException(
            "tests/sqlserver/config.json not found. Copy tests/sqlserver/config.json.example to tests/sqlserver/config.json and set your connection details.");
    }
}
