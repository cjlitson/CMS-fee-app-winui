-- Migration 004: Add CLFS, ASP, OPPS, and ASC fee schedule tables

CREATE TABLE IF NOT EXISTS clfs_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    payment_limit REAL NOT NULL,
    modifier TEXT,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_clfs_code_year ON clfs_fees (hcpcs_code, year);

CREATE TABLE IF NOT EXISTS asp_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    quarter INTEGER NOT NULL,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    payment_limit REAL NOT NULL,
    dosage_descriptor TEXT,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_asp_code_year_quarter ON asp_fees (hcpcs_code, year, quarter);

CREATE TABLE IF NOT EXISTS opps_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    apc_code TEXT,
    payment_rate REAL NOT NULL,
    status_indicator TEXT,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_opps_code_year ON opps_fees (hcpcs_code, year);

CREATE TABLE IF NOT EXISTS asc_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    payment_rate REAL NOT NULL,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_asc_code_year ON asc_fees (hcpcs_code, year);
