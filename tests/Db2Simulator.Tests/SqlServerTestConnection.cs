using Db2Simulator.Config;
using Microsoft.Data.SqlClient;

namespace Db2Simulator.Tests;

internal static class SqlServerTestConnection
{
    public static string BuildConnectionString(SqlServerConnectionConfig config)
    {
        int port = config.Port > 0 ? config.Port : 1433;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{config.Host},{port}",
            InitialCatalog = config.Database,
            UserID = config.User,
            Password = config.Password,
            TrustServerCertificate = true,
        };
        return builder.ConnectionString;
    }

    public static string? Probe(string connectionString)
    {
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            return null;
        }
        catch (Exception ex)
        {
            return $"SQL Server not reachable: {ex.Message}";
        }
    }
}
