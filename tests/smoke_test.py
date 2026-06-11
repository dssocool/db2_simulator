"""Smoke test the DB2 simulator using the pydrda DRDA client (db2 mode)."""
import sys
import drda

HOST = "127.0.0.1"
PORT = 50051
DB = "TESTDB"
USER = "db2inst1"
PASSWORD = "YourStrongPassword123"


def connect(user=USER, password=PASSWORD):
    return drda.connect(host=HOST, database=DB, port=PORT,
                        user=user, password=password)


def test_timestamp():
    conn = connect()
    cur = conn.cursor()
    cur.execute("SELECT CURRENT TIMESTAMP AS TS FROM SYSIBM.SYSDUMMY1")
    rows = cur.fetchall()
    print("timestamp rows:", rows)
    print("description:", cur.description)
    assert len(rows) == 1, rows
    assert cur.description[0][0] == "TS", cur.description
    conn.close()


def test_demo_multi():
    conn = connect()
    cur = conn.cursor()
    cur.execute("SELECT * FROM DEMO")
    rows = cur.fetchall()
    print("demo rows:", rows)
    assert len(rows) == 3, rows
    conn.close()


def test_bad_password():
    try:
        conn = connect(password="wrong")
        cur = conn.cursor()
        cur.execute("SELECT 1 FROM SYSIBM.SYSDUMMY1")
        cur.fetchall()
        print("ERROR: bad password was accepted")
        return False
    except Exception as e:
        print("bad password rejected as expected:", type(e).__name__, e)
        return True


def test_unmapped():
    conn = connect()
    cur = conn.cursor()
    try:
        cur.execute("SELECT * FROM NOSUCHTABLE")
        cur.fetchall()
        print("ERROR: unmapped query did not raise")
        return False
    except Exception as e:
        print("unmapped query raised as expected:", type(e).__name__, e)
        return True
    finally:
        conn.close()


if __name__ == "__main__":
    name = sys.argv[1] if len(sys.argv) > 1 else "all"
    if name in ("all", "timestamp"):
        test_timestamp()
    if name in ("all", "demo"):
        test_demo_multi()
    if name in ("all", "badpw"):
        test_bad_password()
    if name in ("all", "unmapped"):
        test_unmapped()
    print("DONE")
