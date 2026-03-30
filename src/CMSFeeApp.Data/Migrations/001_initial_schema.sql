-- Migration 001: Initial schema

CREATE TABLE IF NOT EXISTS migrations (
    id TEXT NOT NULL PRIMARY KEY,
    applied_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS states (
    abbr TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS selected_states (
    state_abbr TEXT NOT NULL PRIMARY KEY,
    FOREIGN KEY (state_abbr) REFERENCES states(abbr)
);

CREATE TABLE IF NOT EXISTS user_preferences (
    key TEXT NOT NULL PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS import_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    schedule_type TEXT NOT NULL,
    year INTEGER NOT NULL,
    file_path TEXT,
    records_imported INTEGER NOT NULL DEFAULT 0,
    imported_at TEXT NOT NULL,
    success INTEGER NOT NULL DEFAULT 1,
    error_message TEXT
);

CREATE TABLE IF NOT EXISTS dmepos_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    state_abbr TEXT NOT NULL,
    year INTEGER NOT NULL,
    allowable REAL NOT NULL,
    modifier TEXT,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_dmepos_code_year_state
    ON dmepos_fees (hcpcs_code, year, state_abbr);

CREATE TABLE IF NOT EXISTS pfs_fees (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year INTEGER NOT NULL,
    hcpcs_code TEXT NOT NULL,
    description TEXT,
    payment_non_facility REAL NOT NULL,
    payment_facility REAL NOT NULL,
    modifier TEXT,
    data_source TEXT NOT NULL,
    imported_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_pfs_code_year
    ON pfs_fees (hcpcs_code, year);
