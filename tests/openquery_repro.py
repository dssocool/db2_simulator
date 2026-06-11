"""Reproduce the DB2OLEDB / SQL Server OPENQUERY flow against the simulator.

Unlike the no-arg query path in pydrda (PRPSQLSTT -> OPNQRY, columns taken from
QRYDSC), the Microsoft OLE DB Provider for DB2 prepares *with describe*: it sends
PRPSQLSTT with RTNSQLDA=TRUE and expects the column metadata back in an SQLDARD,
and (depending on settings) also issues a standalone DSCSQLSTT. The reply-data
objects (SQLCARD / SQLDARD) must be carried in an OBJDSS (DSS format 3), not an
RPYDSS (format 2); a strict client such as DB2OLEDB aborts the connection
otherwise, surfacing as SQL Server msg 7357 ("the object has no columns ...").

This script drives exactly that flow and asserts the DSS framing + parsed
describe, so the OPENQUERY regression can be checked without SQL Server.

Run:  python tests/openquery_repro.py [port]   (default 50059)
"""
import sys

import drda
from drda import codepoint as cp
from drda import ddm
# DB2OLEDB uses the same describe layout as pydrda's built-in db2 parser.

HOST = "127.0.0.1"
DB = "TESTDB"
USER = "db2inst1"
PASSWORD = "YourStrongPassword123"
QUERY = "SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1"

OBJDSS = 3  # DSS format that must carry reply-data objects (SQLCARD/SQLDARD/QRYDSC/QRYDTA)
RPYDSS = 2


def _read_one(sock):
    return ddm.read_dss(sock, "db2")


def prepare_with_describe(conn):
    """Mimic DB2OLEDB: PRPSQLSTT(RTNSQLDA=TRUE) + SQLSTT, expect SQLDARD in OBJDSS."""
    sock = conn.sock
    cur_id = 1
    cur_id = ddm.write_request_dss(
        sock, ddm.packPRPSQLSTT(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database),
        cur_id, True, False)
    cur_id = ddm.write_request_dss(
        sock, ddm.packSQLSTT(QUERY), cur_id, False, True)

    dss_type, chained, _corr, code_point, obj, _more = _read_one(sock)
    assert code_point == cp.SQLDARD, f"expected SQLDARD, got 0x{code_point:04X}"
    assert dss_type == OBJDSS, (
        f"SQLDARD framed as DSS type {dss_type}, expected OBJDSS({OBJDSS}) -- "
        "this is the framing DB2OLEDB rejects")
    # Drain any remaining chained DSS in this reply.
    while chained:
        dss_type, chained, _corr, _cp, _o, _m = _read_one(sock)
    err, description = ddm.parse_sqldard(obj, "utf-8", conn.endian, "db2")
    assert err is None, f"SQLDARD carried an error: {err}"
    assert len(description) == 1, f"expected 1 column, got {description}"
    name = description[0][0]
    assert name == "TS", f"expected column name 'TS', got {name!r}"
    print(f"prepare-with-describe OK: SQLDARD(OBJDSS), 1 column name={name!r}")
    return description


def describe_standalone(conn):
    """Mimic the separate DSCSQLSTT path, expect SQLDARD in OBJDSS."""
    sock = conn.sock
    cur_id = 1
    cur_id = ddm.write_request_dss(
        sock, ddm.packPRPSQLSTT(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database),
        cur_id, True, False)
    cur_id = ddm.write_request_dss(
        sock, ddm.packSQLSTT(QUERY), cur_id, True, False)
    cur_id = ddm.write_request_dss(
        sock, ddm.packDSCSQLSTT(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database),
        cur_id, False, True)

    saw_sqldard = False
    chained = True
    while chained:
        dss_type, chained, _corr, code_point, obj, _more = _read_one(sock)
        if code_point == cp.SQLDARD:
            saw_sqldard = True
            assert dss_type == OBJDSS, (
                f"DSCSQLSTT SQLDARD framed as DSS type {dss_type}, expected OBJDSS({OBJDSS})")
            err, description = ddm.parse_sqldard(obj, "utf-8", conn.endian, "db2")
            assert err is None and len(description) == 1, description
    assert saw_sqldard, "no SQLDARD returned for DSCSQLSTT"
    print("standalone DSCSQLSTT OK: SQLDARD(OBJDSS)")


def open_query(conn):
    """OPNQRY and read the row, as the provider does after describe."""
    sock = conn.sock
    cur_id = 1
    cur_id = ddm.write_request_dss(
        sock, ddm.packPRPSQLSTT(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database),
        cur_id, True, False)
    cur_id = ddm.write_request_dss(sock, ddm.packSQLSTT(QUERY), cur_id, False, True)
    # drain prepare reply
    chained = True
    while chained:
        _t, chained, _c, _cp, _o, _m = _read_one(sock)

    cur_id = 1
    ddm.write_request_dss(
        sock, ddm.packOPNQRY(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database, conn.qryblksz),
        cur_id, False, True)
    rows, description, _ = conn._parse_response()
    print(f"OPNQRY rows: {list(rows)}")
    assert len(rows) == 1, rows


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 50059
    conn = drda.connect(host=HOST, database=DB, port=port, user=USER, password=PASSWORD)
    try:
        prepare_with_describe(conn)
        describe_standalone(conn)
        open_query(conn)
    finally:
        conn.close()
    print("DONE")


if __name__ == "__main__":
    main()
