using Microsoft.Data.SqlClient;
using SizzlingDb.Config;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>Opens connections to the real SQL Server configured in tests/config.json.</summary>
internal static class SqlServerTestConnection
{
    public static SqlConnection Open(SqlServerConnectionConfig config, string? database = null)
    {
        var conn = new SqlConnection(BuildConnectionString(config, database));
        conn.Open();
        return conn;
    }

    public static string BuildConnectionString(SqlServerConnectionConfig config, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = config.DataSource,
            InitialCatalog = database ?? config.Database,
            UserID = config.User,
            Password = config.Password,
            TrustServerCertificate = true,
            Pooling = false,
        };
        builder["Encrypt"] = "Optional";
        return builder.ConnectionString;
    }
}
