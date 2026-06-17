using SizzlingDb.Backends.SqlServer.Protocol;
using SizzlingDb.Config;
using SizzlingDb.Core;

namespace SizzlingDb.Backends.SqlServer;

internal sealed class SqlServerSimulatorBackend : ISimulatorBackend
{
    private readonly SizzlingDbConfig _config;

    public SqlServerSimulatorBackend(SizzlingDbConfig config) => _config = config;

    public void Run(CancellationToken cancellation)
    {
        var server = new TdsServer(_config);
        server.Run(cancellation);
    }
}
