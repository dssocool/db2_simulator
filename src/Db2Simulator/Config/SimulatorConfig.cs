using System.Text.Json;
using System.Text.Json.Serialization;

namespace Db2Simulator.Config;

/// <summary>Root configuration loaded from config.json.</summary>
public sealed class SimulatorConfig
{
    public ServerConfig Server { get; set; } = new();
    public AuthConfig Auth { get; set; } = new();
    public TraceConfig Trace { get; set; } = new();
    public MatchingConfig Matching { get; set; } = new();
    /// <summary>Optional integration-test connection targets; omit a section to skip those tests.</summary>
    public TestConnectionsConfig Tests { get; set; } = new();
    /// <summary>Populated from default_data.json at startup; tests may set inline.</summary>
    [JsonIgnore]
    public DefaultResponseConfig? DefaultResponse { get; set; }

    /// <summary>Populated from data files at startup; tests may set inline.</summary>
    [JsonIgnore]
    public List<MappingConfig> Mappings { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static SimulatorConfig Load(string path)
    {
        string json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<SimulatorConfig>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Configuration file is empty or invalid.");
        cfg.Validate();
        return cfg;
    }

    private void Validate()
    {
        if (Server.Port is <= 0 or > 65535)
            throw new InvalidOperationException($"Invalid server port: {Server.Port}");
    }
}

public sealed class ServerConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 50000;
    public string Database { get; set; } = "TESTDB";
    public string ServerClassName { get; set; } = "QDB2/LINUXX8664";
    public string ServerName { get; set; } = "DB2SIM";
    public string ProductId { get; set; } = "SQL11055";

    /// <summary>"little" (DB2 LUW x86 / QTDSQLX86) or "big" (QTDSQLASC).</summary>
    public string DataEndian { get; set; } = "little";

    [JsonIgnore]
    public bool LittleEndianData => !string.Equals(DataEndian, "big", StringComparison.OrdinalIgnoreCase);

    /// <summary>TYPDEFNAM advertised to the client in ACCRDBRM.</summary>
    [JsonIgnore]
    public string TypeDefName => LittleEndianData ? "QTDSQLX86" : "QTDSQLASC";
}

public sealed class AuthConfig
{
    public bool RequirePassword { get; set; } = true;
    public bool CaseInsensitiveUser { get; set; } = true;
    public List<UserConfig> Users { get; set; } = new();

    public bool Validate(string user, string password)
    {
        var cmp = CaseInsensitiveUser ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var u in Users)
        {
            if (!string.Equals(u.User, user, cmp))
                continue;
            if (!RequirePassword)
                return true;
            return string.Equals(u.Password, password, StringComparison.Ordinal);
        }
        return false;
    }

    public bool UserExists(string user)
    {
        var cmp = CaseInsensitiveUser ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Users.Exists(u => string.Equals(u.User, user, cmp));
    }
}

public sealed class UserConfig
{
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class TraceConfig
{
    public bool LogCommands { get; set; } = true;
    public bool HexDump { get; set; }
}

public sealed class MatchingConfig
{
    public bool IgnoreCase { get; set; } = true;
    public bool CollapseWhitespace { get; set; } = true;
    public bool TrimTrailingSemicolon { get; set; } = true;
}

public enum MatchKind
{
    Exact,
    Regex,
}

public sealed class MappingConfig
{
    public string Sql { get; set; } = "";
    public MatchKind Match { get; set; } = MatchKind.Exact;
    public ResultConfig? Result { get; set; }
    public long? UpdateCount { get; set; }
    public ErrorConfig? Error { get; set; }

    [JsonIgnore]
    public System.Text.RegularExpressions.Regex? CompiledRegex { get; private set; }

    public void Compile()
    {
        if (Match == MatchKind.Regex)
        {
            CompiledRegex = new System.Text.RegularExpressions.Regex(
                Sql,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.Compiled);
        }
    }
}

public sealed class ResultConfig
{
    public List<ColumnConfig> Columns { get; set; } = new();
    public List<List<JsonElement>> Rows { get; set; } = new();
}

public sealed class ColumnConfig
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "VARCHAR";
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool Nullable { get; set; } = true;
}

public sealed class ErrorConfig
{
    public int Sqlcode { get; set; } = -204;
    public string Sqlstate { get; set; } = "42704";
    public string Message { get; set; } = "";
}

public sealed class DefaultResponseConfig
{
    public ResultConfig? Result { get; set; }
    public long? UpdateCount { get; set; }
    public ErrorConfig? Error { get; set; }
}

public sealed class TestConnectionsConfig
{
    public DatabaseConnectionConfig? Db2 { get; set; }
    public SqlServerConnectionConfig? SqlServer { get; set; }
}

public class DatabaseConnectionConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(Database)
        && !string.IsNullOrWhiteSpace(User);
}

public sealed class SqlServerConnectionConfig : DatabaseConnectionConfig
{
}
