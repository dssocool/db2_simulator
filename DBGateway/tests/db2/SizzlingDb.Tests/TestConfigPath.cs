namespace SizzlingDb.Tests;

internal static class TestConfigPath
{
    public static string Resolve()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "db2", "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DBGateway", "tests", "db2", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tests", "db2", "config.json"),
        ];

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        throw new FileNotFoundException(
            "tests/db2/config.json not found. Copy tests/db2/config.json.example to tests/db2/config.json and set your connection details.");
    }
}
