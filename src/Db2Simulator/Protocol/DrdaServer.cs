using System.Net;
using System.Net.Sockets;
using Db2Simulator.Config;

namespace Db2Simulator.Protocol;

/// <summary>TCP listener that accepts DRDA clients and runs a connection per socket.</summary>
internal sealed class DrdaServer
{
    private readonly SimulatorConfig _config;

    public DrdaServer(SimulatorConfig config) => _config = config;

    public void Run(CancellationToken cancellation)
    {
        IPAddress address = _config.Server.Host == "0.0.0.0" || string.IsNullOrEmpty(_config.Server.Host)
            ? IPAddress.Any
            : IPAddress.Parse(_config.Server.Host);

        var listener = new TcpListener(address, _config.Server.Port);
        listener.Start();
        Console.WriteLine($"DB2 simulator listening on {address}:{_config.Server.Port} " +
                          $"(database={_config.Server.Database}, typedef={_config.Server.TypeDefName})");

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
