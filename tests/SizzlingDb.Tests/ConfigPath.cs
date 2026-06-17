namespace SizzlingDb.Tests;

internal static class ConfigPath
{
    public static string Resolve()
    {
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "config", "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "config", "config.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "config.json"),
        ];

        foreach (string candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        throw new FileNotFoundException(
            "config/config.json not found. Copy config/config.json.example to config/config.json and set your DB2 connection details.");
    }
}
