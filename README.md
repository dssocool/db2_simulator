# DB2 DRDA Simulator

A standalone .NET 10 TCP server that speaks just enough of the **DB2 DRDA wire
protocol** to look like a real IBM DB2 LUW server to the **Microsoft OLE DB
Provider for DB2 (`DB2OLEDB`)**. It performs the full connection handshake,
authenticates the client with user id + password, and answers each incoming SQL
statement with a **fixed, configured result** — it does not parse, execute, or
store any data.

This lets you point a SQL Server linked server (that normally targets DB2) at the
simulator instead, and get back canned results for known statements.

```
SQL Server  ──DB2OLEDB / DRDA──▶  db2sim (this project)  ──▶  config.json + default_data.json [+ data.json]
```

## What it implements

- DRDA/DDM framing (DSS request/reply/object, command chaining, correlation ids).
- Connection handshake: `EXCSAT → ACCSEC → SECCHK → ACCRDB` with their replies.
- Authentication via DRDA security mechanism **USRIDPWD** (plain user id +
  password — the default used by `DB2OLEDB` when no `Authentication` property is
  set). If a client requests the encrypted `EUSRIDPWD` mechanism, the simulator
  renegotiates down to `USRIDPWD`.
- SQL flow: `PRPSQLSTT`, `DSCSQLSTT`, `OPNQRY` / `CNTQRY` / `CLSQRY`,
  `EXCSQLSTT`, `EXCSQLIMM`, `EXCSQLSET`, `RDBCMM` / `RDBRLLBCK`, and package bind
  acknowledgements (`BGNBND` / `BNDSQLSTT` / `ENDBND`).
- FD:OCA result encoding (`SQLDARD`, `QRYDSC`, `QRYDTA`, `SQLCARD`) for the DB2
  LUW on Intel dialect (`QTDSQLX86`, little-endian data, SQLAM level 7).
- Column types: `CHAR`, `VARCHAR`, `SMALLINT`, `INTEGER`, `BIGINT`, `REAL`,
  `DOUBLE`, `DECIMAL(p,s)`, `DATE`, `TIME`, `TIMESTAMP`, plus `NULL` values.

## Requirements

- .NET SDK 10
- IBM Db2 .NET driver (`Net.IBM.Data.Db2` / `Net.IBM.Data.Db2-lnx`) for integration tests

## Build & run

```bash
dotnet build
dotnet run --project src/Db2Simulator -- config/config.json
```

The configuration path is optional; if omitted the server looks for
`config/config.json` next to the binary and in the current directory. You can
also pass `--config <path>`. Built-in SQL mappings and the default unmapped
response are always loaded from `default_data.json` (next to the config file or
under `config/`). Optional user mappings are merged from `data.json` when that
file exists.

On start it prints:

```
DB2 simulator listening on 0.0.0.0:50000 (database=TESTDB, typedef=QTDSQLX86)
```

The server binds `0.0.0.0` so it is reachable from the Windows host whether SQL
Server reaches WSL2 via `localhost` forwarding or the WSL IP.

## Configuration

Server settings live in `config/config.json`. Built-in mappings and the
default unmapped response live in `config/default_data.json` (shipped with the
project). Add your own mappings in `config/data.json` (gitignored). Copy
`config/config.json.example` to create your local server settings file.

### Server settings (`config/config.json`)

```jsonc
{
  "server": {
    "host": "0.0.0.0",
    "port": 50000,
    "database": "TESTDB",            // must match Initial Catalog in the linked server
    "serverClassName": "QDB2/LINUXX8664",
    "serverName": "DB2SIM",
    "productId": "SQL11055",         // advertised PRDID (DB2 LUW v11.x)
    "dataEndian": "little"           // "little" = QTDSQLX86, "big" = QTDSQLASC
  },
  "auth": {
    "requirePassword": true,
    "caseInsensitiveUser": true,
    "users": [
      { "user": "db2inst1", "password": "YourStrongPassword123" }
    ]
  },
  "trace": {
    "logCommands": true,             // one log line per DRDA command
    "hexDump": false                 // set true to dump every inbound/outbound DSS
  },
  "matching": {
    "ignoreCase": true,
    "collapseWhitespace": true,      // treat runs of whitespace as one space
    "trimTrailingSemicolon": true
  },
  "tests": {
    "db2": {                         // optional — omit to skip real-DB2 tests
      "host": "127.0.0.1",
      "port": 50000,
      "database": "SAMPLE",
      "user": "db2inst1",
      "password": "YourStrongPassword123"
    },
    "sqlServer": {                 // optional — omit to skip SQL Server tests
      "host": "localhost\\SQLEXPRESS", // named instance; omit port to use SQL Browser
      "database": "master",
      "user": "dev_user",
      "password": "YourStrongPassword123"
    }
  }
}
```

The top-level `server`, `auth`, `trace`, and `matching` sections configure the simulator
process (`dotnet run`). The optional `tests` section holds connection details for
integration tests against real databases; omit `tests.db2` or `tests.sqlServer` (or leave
`tests` empty) and those tests are skipped automatically. For SQL Server Express named
instances with a dynamic port, set `host` to `server\\instance` and omit `port` so the
client resolves the port via SQL Browser; set `port` only when you know the fixed or
dynamic TCP port.

### Built-in data (`config/default_data.json`)

Always loaded at startup. Contains the default unmapped response and the
`CURRENT TIMESTAMP` probe mapping used by DB2OLEDB during connection setup.

```jsonc
{
  "defaultResponse": {
    "error": { "sqlcode": -204, "sqlstate": "42704", "message": "..." }
  },
  "mappings": [
    {
      "sql": "SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1",
      "match": "exact",
      "result": {
        "columns": [ { "name": "TS", "type": "TIMESTAMP", "nullable": true } ],
        "rows": [ ["2026-06-10-14.30.00.000000"] ]
      }
    }
  ]
}
```

### User mappings (`config/data.json`)

Optional. Create this file to add your own SQL-to-result mappings; entries are
merged after the built-in mappings from `default_data.json`.

```jsonc
{
  "mappings": [
    {
      "sql": "^SELECT \\* FROM DEMO\\b",
      "match": "regex",
      "result": {
        "columns": [
          { "name": "ID", "type": "INTEGER", "nullable": false },
          { "name": "NAME", "type": "VARCHAR", "length": 50, "nullable": true }
        ],
        "rows": [ [1, "Widget"], [2, "Gadget"] ]
      }
    }
  ]
}
```

### Mapping rules

Each entry in `mappings` has a `sql` and a `match` (`exact` or `regex`) plus one
of:

- `result` — a fixed result set (`columns` + `rows`).
- `updateCount` — return success with this many rows affected (for INSERT/UPDATE/DELETE-style statements).
- `error` — return a DB2 error (`sqlcode`, `sqlstate`, `message`).

For `exact` matches, both the configured and incoming SQL are normalized
(trimmed, whitespace-collapsed, upper-cased, trailing `;` removed) per the
`matching` settings. For `regex`, the `sql` field is a .NET regular expression
matched case-insensitively against the statement.

`defaultResponse` in `default_data.json` is returned for any statement that
matches no mapping; if it is omitted, unmapped statements return SQLCODE `-204` /
SQLSTATE `42704`.

### Column definition

```jsonc
{ "name": "PRICE", "type": "DECIMAL", "precision": 9, "scale": 2, "nullable": true }
```

| type        | row value example                  | notes                              |
|-------------|------------------------------------|------------------------------------|
| `CHAR`/`VARCHAR` | `"Widget"`                    | `length` optional                  |
| `SMALLINT`/`INTEGER`/`BIGINT` | `42`              |                                    |
| `REAL`/`DOUBLE` | `3.14`                         |                                    |
| `DECIMAL`   | `"19.99"` or `19.99`               | set `precision` and `scale`        |
| `DATE`      | `"2026-01-15"`                     | `YYYY-MM-DD`                        |
| `TIME`      | `"09.30.00"`                       | `HH.MM.SS`                         |
| `TIMESTAMP` | `"2026-06-10-14.30.00.000000"`     | `YYYY-MM-DD-HH.MM.SS.ffffff`       |

Use JSON `null` for a NULL cell.

## Testing

Copy `config/config.json.example` to `config/config.json` before running tests.
Simulator-focused tests start an embedded simulator using the top-level `server`
and `auth` settings and supply their own SQL-to-result mappings inline — they do
not read `default_data.json` or `data.json`.

Tests that target a real DB2 or SQL Server read optional connection details from
`tests.db2` and `tests.sqlServer`. Omit either section (or leave `tests` empty)
and tests for that target are skipped. OPENQUERY tests connect to SQL Server,
verify that the Microsoft `DB2OLEDB` provider is installed, create a temporary
linked server from `tests.db2`, and drop it when the test session ends.

```bash
dotnet test tests/Db2Simulator.Tests
```

Tests are also skipped when `config.json` is missing or a configured endpoint is
unreachable. On Linux the IBM `clidriver` native libraries bundled with the test
project are configured automatically; a Db2Connect license may be required for
non-trial deployments.

## Using it from SQL Server

Point your existing linked server at the simulator's host/port (the simulator
defaults to the same `127.0.0.1:50000`, `Initial Catalog=testdb` used by a real
DB2 link). Make sure the configured `database`, `user`, and `password` match the
linked server's `Initial Catalog` and remote login.

```sql
EXEC master.dbo.sp_addlinkedserver
    @server = N'DB2LS', @srvproduct = N'DB2', @provider = N'DB2OLEDB',
    @datasrc = N'127.0.0.1',
    @provstr = N'Network Address=127.0.0.1;Network Port=50000;Initial Catalog=testdb;Package Collection=NULLID;Default Schema=DB2INST1;';

EXEC master.dbo.sp_addlinkedsrvlogin
    @rmtsrvname = N'DB2LS', @useself = N'False', @locallogin = NULL,
    @rmtuser = N'db2inst1', @rmtpassword = N'YourStrongPassword123';

SELECT * FROM OPENQUERY(DB2LS, 'SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1');
```

If a statement does not come back as expected, enable `"hexDump": true` in the
trace section and restart — the server then logs every inbound and outbound DSS
as a hex dump, which makes it straightforward to see exactly what the provider
sent and to add or adjust a mapping.

## Limitations / notes

- Only the plain `USRIDPWD` security mechanism is implemented. If the linked
  server is configured with `Authentication=Server_Encrypt_Pwd` (or similar), the
  DES/Diffie-Hellman encrypted mechanisms would need to be added.
- Each answer set is returned in a single query block; a single statement's
  result must fit in one ~32 KB DSS. This is ample for canned responses but is not
  meant for very large result sets.
- The simulator emulates the **DB2 LUW on Intel** dialect (`QTDSQLX86`,
  little-endian data, EBCDIC CCSID 500 for protocol strings, UTF-8 / CCSID 1208
  for character data). Switch to `"dataEndian": "big"` only if your client expects
  the `QTDSQLASC` type definition.
```
