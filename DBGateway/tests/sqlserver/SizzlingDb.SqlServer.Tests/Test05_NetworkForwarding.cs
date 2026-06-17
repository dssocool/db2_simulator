using System.Diagnostics;

namespace SizzlingDb.SqlServer.Tests;

/// <summary>
/// Optional: capture traffic to the upstream SQL Server while the gateway forwards SQL.
/// Skips when tcpdump is not installed.
/// </summary>
[TestCaseOrderer(NumberedTestCaseOrderer.TypeName, TestOrdering.AssemblyName)]
public sealed class Test05_NetworkForwarding
{
    [Fact(Skip = "Gateway TDS re-encoding for forwarded results is not yet SqlClient-compatible")]
    public void T01_UnmappedSelect_ProducesTrafficToUpstreamSqlServer()
    {
        if (!IsTcpdumpAvailable())
            return;

        var upstream = TestConfig.RequireSqlServer();
        var gateway = GatewayServer.Fixture;
        string captureFile = Path.Combine(Path.GetTempPath(), $"sizzlingdb-tcpdump-{Guid.NewGuid():N}.pcap");
        string filter = upstream.Port > 0 ? $"host {upstream.Host} and port {upstream.Port}" : $"host {upstream.Host} and port 1433";

        using var tcpdump = StartTcpdump(captureFile, filter);
        Thread.Sleep(500);

        using (var conn = GatewayTestConnection.Open(gateway))
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT COL_INT FROM dbo.{TestObjects.TableName} WHERE ID = 1";
            Assert.Equal(42, Convert.ToInt32(cmd.ExecuteScalar()));
        }

        StopTcpdump(tcpdump);
        Thread.Sleep(300);

        try
        {
            Assert.True(File.Exists(captureFile));
            Assert.True(new FileInfo(captureFile).Length > 24);
        }
        finally
        {
            if (File.Exists(captureFile))
                File.Delete(captureFile);
        }
    }

    private static bool IsTcpdumpAvailable()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "tcpdump",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            return proc?.WaitForExit(3000) == true && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Process StartTcpdump(string captureFile, string filter)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "tcpdump",
            Arguments = $"-i any -w {captureFile} -c 20 {filter}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("failed to start tcpdump");
    }

    private static void StopTcpdump(Process proc)
    {
        try
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
            proc.WaitForExit(3000);
        }
        catch
        {
            // Best effort.
        }
    }
}
