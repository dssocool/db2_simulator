using SizzlingDb.Backends.Db2;
using SizzlingDb.Config;
using SizzlingDb.Core;

string configPath = ResolveConfigPath(args);

SizzlingDbConfig config;
try
{
    config = SizzlingDbConfig.Load(configPath);
    string backendType = config.Database.Type.Trim().ToLowerInvariant();

    string defaultDataPath = ResolveDefaultDataPath(configPath, backendType);
    string? userDataPath = ResolveUserDataPath(configPath, backendType);

    Console.WriteLine($"Loading configuration: {configPath}");
    Console.WriteLine($"Loading default mappings: {defaultDataPath}");
    if (userDataPath is not null)
        Console.WriteLine($"Loading user mappings: {userDataPath}");

    MappingData defaultData = MappingData.Load(defaultDataPath);
    config.DefaultResponse = defaultData.DefaultResponse;
    config.Mappings = defaultData.Mappings;

    if (userDataPath is not null)
    {
        MappingData userData = MappingData.Load(userDataPath);
        config.Mappings.AddRange(userData.Mappings);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load configuration: {ex.Message}");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Shutting down...");
    cts.Cancel();
};

ISimulatorBackend backend = config.Database.Type.Trim().ToLowerInvariant() switch
{
    "db2" => new Db2SimulatorBackend(config),
    _ => throw new InvalidOperationException($"Unsupported database.type: {config.Database.Type}"),
};

try
{
    backend.Run(cts.Token);
}
catch (Exception ex) when (!cts.IsCancellationRequested)
{
    Console.Error.WriteLine($"Server error: {ex.Message}");
    return 1;
}

return 0;

static string ResolveConfigPath(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--config" or "-c")
            return args[i + 1];
    }
    if (args.Length == 1 && !args[0].StartsWith('-'))
        return args[0];

    string[] candidates =
    {
        Path.Combine(AppContext.BaseDirectory, "config", "config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "config.json.example"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "config.json.example"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return candidates[0];
}

static string ResolveDefaultDataPath(string configPath, string backendType)
{
    string? dir = Path.GetDirectoryName(Path.GetFullPath(configPath));
    if (string.IsNullOrEmpty(dir))
        dir = Directory.GetCurrentDirectory();

    string[] candidates =
    {
        Path.Combine(dir, "backends", backendType, "default_data.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "backends", backendType, "default_data.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "backends", backendType, "default_data.json"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return candidates[0];
}

static string? ResolveUserDataPath(string configPath, string backendType)
{
    string? dir = Path.GetDirectoryName(Path.GetFullPath(configPath));
    if (string.IsNullOrEmpty(dir))
        dir = Directory.GetCurrentDirectory();

    string[] candidates =
    {
        Path.Combine(dir, "backends", backendType, "data.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "backends", backendType, "data.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "backends", backendType, "data.json"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return null;
}
