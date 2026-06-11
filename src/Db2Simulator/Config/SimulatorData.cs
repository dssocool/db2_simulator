using System.Text.Json;
using System.Text.Json.Serialization;

namespace Db2Simulator.Config;

/// <summary>SQL-to-result mappings and default response loaded from data files.</summary>
public sealed class SimulatorData
{
    public DefaultResponseConfig? DefaultResponse { get; set; }
    public List<MappingConfig> Mappings { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static SimulatorData Load(string path)
    {
        string json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<SimulatorData>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Data file is empty or invalid.");
        data.Validate();
        return data;
    }

    private void Validate()
    {
        foreach (var m in Mappings)
            m.Compile();
    }
}
