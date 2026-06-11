using Db2Simulator.Config;
using Db2Simulator.Protocol;

string configPath = ResolveConfigPath(args);
string defaultDataPath = ResolveDefaultDataPath(configPath);
string? userDataPath = ResolveUserDataPath(configPath);
Console.WriteLine($"Loading configuration: {configPath}");
Console.WriteLine($"Loading default mappings: {defaultDataPath}");
if (userDataPath is not null)
    Console.WriteLine($"Loading user mappings: {userDataPath}");

SimulatorConfig config;
try
{
    config = SimulatorConfig.Load(configPath);
    SimulatorData defaultData = SimulatorData.Load(defaultDataPath);
    config.DefaultResponse = defaultData.DefaultResponse;
    config.Mappings = defaultData.Mappings;

    if (userDataPath is not null)
    {
        SimulatorData userData = SimulatorData.Load(userDataPath);
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

var server = new DrdaServer(config);
try
{
    server.Run(cts.Token);
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

static string ResolveDefaultDataPath(string configPath)
{
    string? dir = Path.GetDirectoryName(Path.GetFullPath(configPath));
    if (string.IsNullOrEmpty(dir))
        dir = Directory.GetCurrentDirectory();

    string[] candidates =
    {
        Path.Combine(dir, "default_data.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "default_data.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "default_data.json"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return candidates[0];
}

static string? ResolveUserDataPath(string configPath)
{
    string? dir = Path.GetDirectoryName(Path.GetFullPath(configPath));
    if (string.IsNullOrEmpty(dir))
        dir = Directory.GetCurrentDirectory();

    string[] candidates =
    {
        Path.Combine(dir, "data.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "data.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "data.json"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return null;
}
