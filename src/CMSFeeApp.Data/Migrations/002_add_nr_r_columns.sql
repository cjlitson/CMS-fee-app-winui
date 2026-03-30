-- Migration 002: Add non-rural and rural allowable columns to dmepos_fees

ALTER TABLE dmepos_fees ADD COLUMN allowable_nr REAL;
ALTER TABLE dmepos_fees ADD COLUMN allowable_r REAL;
