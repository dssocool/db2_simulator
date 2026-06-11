using Db2Simulator.Config;
using Db2Simulator.Protocol;

string configPath = ResolveConfigPath(args);
Console.WriteLine($"Loading configuration: {configPath}");

SimulatorConfig config;
try
{
    config = SimulatorConfig.Load(configPath);
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
        Path.Combine(AppContext.BaseDirectory, "config", "mappings.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "mappings.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "mappings.json"),
        Path.Combine(AppContext.BaseDirectory, "config", "mappings.json.example"),
        Path.Combine(Directory.GetCurrentDirectory(), "config", "mappings.json.example"),
    };
    foreach (string c in candidates)
        if (File.Exists(c))
            return c;
    return candidates[0];
}
