using System.Net;
using System.Net.Sockets;
using SizzlingDb.Config;

namespace SizzlingDb.Backends.SqlServer.Protocol;

/// <summary>TCP listener that accepts TDS clients and runs a connection per socket.</summary>
internal sealed class TdsServer
{
    private readonly SizzlingDbConfig _config;

    public TdsServer(SizzlingDbConfig config) => _config = config;

    public void Run(CancellationToken cancellation)
    {
        SqlServerBackendConfig sql = _config.RequireSqlServer();
        IPAddress address = sql.Host == "0.0.0.0" || string.IsNullOrEmpty(sql.Host)
            ? IPAddress.Any
            : IPAddress.Parse(sql.Host);

        var listener = new TcpListener(address, sql.Port);
        listener.Start();
        Console.WriteLine($"sizzlingdb ({sql.ServerName}) listening on {address}:{sql.Port} " +
                          $"(database={sql.Database}, product={sql.ProductName})");

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
            var connection = new TdsConnection(stream, _config, log);
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
