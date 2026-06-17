using Microsoft.Data.SqlClient;
using SizzlingDb.Config;

namespace SizzlingDb.SqlServer.Tests;

internal static class SimulatorTestConnection
{
    public static SqlConnection Open(SimulatorFixture fixture, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{fixture.Port}",
            InitialCatalog = database ?? fixture.Config.RequireSqlServer().Database,
            UserID = fixture.Config.Auth.Users[0].User,
            Password = fixture.Config.Auth.Users[0].Password,
            TrustServerCertificate = true,
            Pooling = false,
        };
        builder["Encrypt"] = "False";
        var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();
        return conn;
    }
}
