namespace SizzlingDb.Tests;

internal static class TestConfigPath
{
    public static string Resolve()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "DBGateway", "tests", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tests", "config.json"),
        ];

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        throw new FileNotFoundException(
            "tests/config.json not found. Copy tests/config.json.example to tests/config.json and set your connection details.");
    }
}
