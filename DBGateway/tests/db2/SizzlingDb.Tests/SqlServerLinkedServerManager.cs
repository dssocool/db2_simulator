using SizzlingDb.Config;
using Microsoft.Data.SqlClient;

namespace SizzlingDb.Tests;

/// <summary>Creates and drops the DB2OLEDB linked server used by the test suite.</summary>
internal static class SqlServerLinkedServerManager
{
    public static void Recreate(SqlConnection connection, string serverName, DatabaseConnectionConfig db2)
    {
        Drop(connection, serverName);

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

    public static bool Exists(SqlConnection connection, string serverName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sys.servers WHERE name = @server";
        cmd.Parameters.AddWithValue("@server", serverName);
        return (int)cmd.ExecuteScalar()! > 0;
    }

    /// <summary>
    /// Asks SQL Server to open a connection through the linked server; throws with
    /// the provider error when DB2OLEDB is missing or DB2 is unreachable.
    /// </summary>
    public static void TestLink(SqlConnection connection, string serverName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC master.dbo.sp_testlinkedserver @servername = @server";
        cmd.Parameters.AddWithValue("@server", serverName);
        cmd.ExecuteNonQuery();
    }

    private static string BuildProvstr(DatabaseConnectionConfig db2) =>
        $"Network Address={db2.Host};Network Port={db2.Port};Initial Catalog={db2.Database};" +
        $"Package Collection=NULLID;Default Schema={db2.User.ToUpperInvariant()};";
}
