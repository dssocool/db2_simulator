# sizzlingdb

Monorepo for database simulation and gateway components.

## Projects

| Path | Description |
|------|-------------|
| [DBGateway/](DBGateway/) | TCP database gateway / wire-protocol simulator (DB2 DRDA, SQL Server TDS) |

Build and run from the repository root:

```bash
dotnet build
dotnet run --project DBGateway/src/SizzlingDb -- DBGateway/config/config.json
```

See [DBGateway/README.md](DBGateway/README.md) for full documentation.
