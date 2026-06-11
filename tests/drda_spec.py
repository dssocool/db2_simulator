"""Spec-compliant DRDA SQLDAGRP column parser (DRDA V4, Figures 5-36 / 5-39).

pydrda 0.5.2's ``drda.ddm._parse_column_db2`` is not spec-compliant for DB2: it
hard-skips 6 bytes before, and 7 bytes after, the column name fields (see its
``?? skip`` comments). That happens to limp along against the byte layout the
simulator *used* to emit, but it cannot parse a spec-correct SQLDARD.

This module provides a correct parser and a helper to monkeypatch pydrda so the
existing high-level tests can validate the simulator's spec-compliant describe
output (including the real column name).
"""
from drda.ddm import parse_name, parse_string


def _skip_null_group(b):
    """Consume a nullable nested group that the simulator always sends as null."""
    indicator = b[0]
    if indicator != 0xFF:
        raise ValueError(f"expected null nested group (0xFF), got 0x{indicator:02X}")
    return b[1:]


def _skip_sqldxgrp(b):
    """Consume the present SQLDXGRP (extended column attributes) the simulator emits.

    Layout (DRDA V4): null indicator(1) + SQLXKEYMEM/SQLXUPDATEABLE/SQLXGENERATED/
    SQLXPARMMODE (4 x I2) = 9 fixed bytes, then SQLXRDBNAM (VCS) and SQLXCORNAME,
    SQLXBASENAME, SQLXSCHEMA, SQLXNAME (each VCM+VCS). All sent empty.
    """
    indicator = b[0]
    if indicator != 0x00:
        raise ValueError(f"expected present SQLDXGRP (0x00), got 0x{indicator:02X}")
    b = b[9:]
    _, b = parse_string(b)  # SQLXRDBNAM
    _, b = parse_name(b)    # SQLXCORNAME
    _, b = parse_name(b)    # SQLXBASENAME
    _, b = parse_name(b)    # SQLXSCHEMA
    _, b = parse_name(b)    # SQLXNAME
    return b


def parse_column_db2(b, endian, has_name):
    """Parse one SQLDAGRP entry per the DRDA V4 spec (standard describe).

    Layout: SQLPRECISION(2) SQLSCALE(2) SQLLENGTH(8) SQLTYPE(2) SQLCCSID(2),
    then SQLDOPTGRP (nullable): SQLUNNAMED(2), SQLNAME/SQLLABEL/SQLCOMMENTS
    (each VCM+VCS), then SQLUDTGRP (null) and a present SQLDXGRP.

    Returns a tuple matching pydrda's description shape, but with the real
    SQLNAME as the first element.
    """
    precision = int.from_bytes(b[0:2], byteorder=endian)
    scale = int.from_bytes(b[2:4], byteorder=endian)
    sqllength = int.from_bytes(b[4:12], byteorder=endian)
    sqltype = int.from_bytes(b[12:14], byteorder=endian)
    _sqlccsid = int.from_bytes(b[14:16], byteorder="big")
    b = b[16:]

    sqlname = None
    indicator = b[0]
    b = b[1:]
    if indicator == 0x00:  # SQLDOPTGRP present
        _sqlunnamed = int.from_bytes(b[0:2], byteorder=endian)
        b = b[2:]
        sqlname, b = parse_name(b)      # SQLNAME_m / SQLNAME_s
        _sqllabel, b = parse_name(b)    # SQLLABEL_m / SQLLABEL_s
        _sqlcomments, b = parse_name(b) # SQLCOMMENTS_m / SQLCOMMENTS_s
        b = _skip_null_group(b)         # SQLUDTGRP (null)
        b = _skip_sqldxgrp(b)           # SQLDXGRP (present)

    return (sqlname, sqltype, sqllength, sqllength, precision, scale, None), b


def patch_pydrda():
    """Replace pydrda's non-compliant db2 column parser with the spec parser."""
    import drda.ddm as _ddm
    _ddm._parse_column_db2 = parse_column_db2
