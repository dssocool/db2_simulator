"""Dump raw SQLDARD bytes from a DRDA server for comparison."""
import sys

import drda
from drda import codepoint as cp
from drda import ddm

HOST = "127.0.0.1"
DB = "TESTDB"
USER = "db2inst1"
PASSWORD = "YourStrongPassword123"
QUERY = "SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1"


def dump_sqldard(port: int, label: str):
    conn = drda.connect(host=HOST, database=DB, port=port, user=USER, password=PASSWORD)
    try:
        sock = conn.sock
        cur_id = 1
        cur_id = ddm.write_request_dss(
            sock, ddm.packPRPSQLSTT(conn.pkgid, conn.pkgcnstkn, conn.pkgsn, conn.database),
            cur_id, True, False)
        cur_id = ddm.write_request_dss(sock, ddm.packSQLSTT(QUERY), cur_id, False, True)

        dss_type, chained, _corr, code_point, obj, _more = ddm.read_dss(sock, "db2")
        assert code_point == cp.SQLDARD, hex(code_point)
        print(f"=== {label} port {port} DSS type={dss_type} SQLDARD len={len(obj)} ===")
        for i in range(0, len(obj), 16):
            chunk = obj[i:i + 16]
            hexpart = " ".join(f"{b:02X}" for b in chunk)
            print(f"  {i:04X}  {hexpart}")
        print()
    finally:
        conn.close()


if __name__ == "__main__":
    ports = sys.argv[1:] or ["50000", "50001"]
    for p in ports:
        dump_sqldard(int(p), "real" if p == "50000" else "sim")
