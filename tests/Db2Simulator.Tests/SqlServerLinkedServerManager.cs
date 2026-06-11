using Db2Simulator.Config;
using Microsoft.Data.SqlClient;

namespace Db2Simulator.Tests;

internal static class SqlServerLinkedServerManager
{
    private const string ProviderName = "DB2OLEDB";

    public static string? CheckProvider(SqlConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CASE
                WHEN EXISTS (SELECT 1 FROM sys.sysoledb_providers WHERE name = @name) THEN 1
                WHEN EXISTS (SELECT 1 FROM sys.providers WHERE name = @name) THEN 1
                ELSE 0
            END
            """;
        cmd.Parameters.AddWithValue("@name", ProviderName);
        object? result = cmd.ExecuteScalar();
        if (result is 1 or (int)1)
            return null;

        return $"{ProviderName} provider is not installed on SQL Server";
    }

    public static string GenerateServerName() =>
        "DB2LS_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public static void Create(SqlConnection connection, string serverName, DatabaseConnectionConfig db2)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            EXEC master.dbo.sp_addlinkedserver
                @server = @server,
                @srvproduct = N'DB2',
                @provider = N'DB2OLEDB',
                @datasrc = @datasrc,
                @provstr = @provstr;

            EXEC master.dbo.sp_droplinkedsrvlogin
                @rmtsrvname = @server,
                @locallogin = NULL;

            EXEC master.dbo.sp_addlinkedsrvlogin
                @rmtsrvname = @server,
                @useself = N'False',
                @locallogin = NULL,
                @rmtuser = @rmtuser,
                @rmtpassword = @rmtpassword;
            """;
        cmd.Parameters.AddWithValue("@server", serverName);
        cmd.Parameters.AddWithValue("@datasrc", db2.Host);
        cmd.Parameters.AddWithValue("@provstr", BuildProvstr(db2));
        cmd.Parameters.AddWithValue("@rmtuser", db2.User);
        cmd.Parameters.AddWithValue("@rmtpassword", db2.Password);
        cmd.ExecuteNonQuery();
    }

    public static void Drop(SqlConnection connection, string serverName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF EXISTS (SELECT 1 FROM sys.servers WHERE name = @server)
                EXEC master.dbo.sp_dropserver @server = @server, @droplogins = 'droplogins';
            """;
        cmd.Parameters.AddWithValue("@server", serverName);
        cmd.ExecuteNonQuery();
    }

    public static string BuildProvstr(DatabaseConnectionConfig db2) =>
        $"Network Address={db2.Host};Network Port={db2.Port};Initial Catalog={db2.Database};Package Collection=NULLID;Default Schema={db2.User.ToUpperInvariant()};";
}
