using SizzlingDb.Config;
using Microsoft.Data.SqlClient;

namespace SizzlingDb.Tests;

/// <summary>Opens connections to the SQL Server configured in tests.sqlServer.</summary>
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
        };
        return builder.ConnectionString;
    }
}
