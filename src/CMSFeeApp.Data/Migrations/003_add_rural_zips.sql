-- Migration 003: Add rural_zips table for DMEPOS rural pricing lookups

CREATE TABLE IF NOT EXISTS rural_zips (
    year INTEGER NOT NULL,
    zip5 TEXT NOT NULL,
    state_abbr TEXT,
    PRIMARY KEY (year, zip5)
);
