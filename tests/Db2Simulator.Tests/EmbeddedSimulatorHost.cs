using System.Net;
using System.Net.Sockets;
using Db2Simulator.Config;
using Db2Simulator.Protocol;

namespace Db2Simulator.Tests;

internal sealed class EmbeddedSimulatorHost : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;

    public int Port { get; }

    public EmbeddedSimulatorHost(SimulatorConfig config)
    {
        config.Server.Host = "127.0.0.1";
        config.Trace.LogCommands = false;
        config.Trace.HexDump = false;

        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        Port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        config.Server.Port = Port;
        foreach (MappingConfig mapping in config.Mappings)
            mapping.Compile();

        var server = new DrdaServer(config);
        _serverTask = Task.Run(() => server.Run(_cts.Token));
        WaitForListen();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _serverTask.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
        _cts.Dispose();
    }

    private void WaitForListen()
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                using var client = new TcpClient();
                client.Connect(IPAddress.Loopback, Port);
                return;
            }
            catch (SocketException) when (i < 49)
            {
                Thread.Sleep(20);
            }
        }

        throw new InvalidOperationException($"Embedded simulator did not start on port {Port}");
    }
}
