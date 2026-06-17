using SizzlingDb.Backends.Db2.Protocol;
using SizzlingDb.Config;
using SizzlingDb.Core;

namespace SizzlingDb.Backends.Db2;

internal sealed class Db2SimulatorBackend : ISimulatorBackend
{
    private readonly SizzlingDbConfig _config;

    public Db2SimulatorBackend(SizzlingDbConfig config) => _config = config;

    public void Run(CancellationToken cancellation)
    {
        var server = new DrdaServer(_config);
        server.Run(cancellation);
    }
}
