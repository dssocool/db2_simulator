using Microsoft.Data.SqlClient;

namespace SizzlingDb.SqlServer.Tests;

internal static class GatewayTestConnection
{
    public static SqlConnection Open(GatewayFixture fixture, string? database = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{fixture.Port}",
            InitialCatalog = database ?? TestObjects.SqlServerDatabase,
            UserID = fixture.Upstream.User,
            Password = fixture.Upstream.Password,
            TrustServerCertificate = true,
            Pooling = false,
        };
        builder["Encrypt"] = "False";
        var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();
        return conn;
    }
}
