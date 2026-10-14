-- REVIEW ONLY. Prepared for the separate database/schema owner; NOT applied by this port.
-- Live read-only audit on 2026-10-14 found OldStatus NOT NULL.
-- The required initial lifecycle event is SQL NULL -> Pending.
-- This changes only nullability; it creates no tables and changes no existing rows.
BEGIN;
ALTER TABLE public."MaintenanceLogs"
    ALTER COLUMN "OldStatus" DROP NOT NULL;
COMMIT;
