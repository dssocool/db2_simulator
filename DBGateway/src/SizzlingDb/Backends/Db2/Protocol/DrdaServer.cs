using System.Net;
using System.Net.Sockets;
using SizzlingDb.Config;

namespace SizzlingDb.Backends.Db2.Protocol;

/// <summary>TCP listener that accepts DRDA clients and runs a connection per socket.</summary>
internal sealed class DrdaServer
{
    private readonly SizzlingDbConfig _config;

    public DrdaServer(SizzlingDbConfig config) => _config = config;

    public void Run(CancellationToken cancellation)
    {
        Db2BackendConfig db2 = _config.RequireDb2();
        IPAddress address = db2.Host == "0.0.0.0" || string.IsNullOrEmpty(db2.Host)
            ? IPAddress.Any
            : IPAddress.Parse(db2.Host);

        var listener = new TcpListener(address, db2.Port);
        listener.Start();
        Console.WriteLine($"sizzlingdb ({db2.ServerName}) listening on {address}:{db2.Port} " +
                          $"(database={db2.Database}, typedef={db2.TypeDefName})");

        cancellation.Register(() =>
        {
            try { listener.Stop(); } catch { /* ignore */ }
        });

        while (!cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (SocketException) when (cancellation.IsCancellationRequested)
            {
                break;
            }

            var thread = new Thread(() => HandleClient(client)) { IsBackground = true };
            thread.Start();
        }
    }

    private void HandleClient(TcpClient client)
    {
        string peer = client.Client.RemoteEndPoint?.ToString() ?? "?";
        var log = new TraceLogger(_config.Trace.LogCommands, _config.Trace.HexDump, peer);
        log.Info("connection accepted");
        try
        {
            client.NoDelay = true;
            using NetworkStream stream = client.GetStream();
            var connection = new DrdaConnection(stream, _config, log);
            connection.Run();
        }
        catch (IOException ex)
        {
            log.Info($"connection closed: {ex.Message}");
        }
        catch (Exception ex)
        {
            log.Info($"error: {ex}");
        }
        finally
        {
            client.Dispose();
        }
    }
}
