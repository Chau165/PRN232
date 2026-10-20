-- DRAFT / READ ONLY PREFLIGHT. NOT A MAINTENANCE REMEDIATION SCRIPT.
-- Run only after separate owner/DB approval, using the configured live connection.
-- This file intentionally contains no DDL or DML and never edits __EFMigrationsHistory.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10000ms';

-- Confirm migration-order hazard and the already-applied later migration.
SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
WHERE "MigrationId" IN (
    '20261013192052_ReconcileMaintenanceReportsAndLogNullability',
    '20261019034905_AddPropertyPrimaryImageUrl'
)
ORDER BY "MigrationId";

-- Compare the source migration's OldStatus nullability with live catalog.
SELECT table_name, column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND (table_name = 'MaintenanceLogs' AND column_name = 'OldStatus'
       OR table_name = 'MaintenanceReports' AND column_name = 'CreatedAt')
ORDER BY table_name, ordinal_position;

-- Review current Maintenance constraints and exact index definitions.
SELECT table_name, constraint_name, constraint_type
FROM information_schema.table_constraints
WHERE table_schema = 'public'
  AND table_name IN ('MaintenanceReports', 'MaintenanceReportImages')
ORDER BY table_name, constraint_name;

SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename IN ('MaintenanceReports', 'MaintenanceReportImages')
ORDER BY tablename, indexname;

-- Verify RLS state and policy counts without reading row content.
SELECT c.relname AS table_name,
       c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS rls_forced,
       count(p.policyname) AS policy_count
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_policies p ON p.schemaname = n.nspname AND p.tablename = c.relname
WHERE n.nspname = 'public'
  AND c.relname IN ('MaintenanceReports', 'MaintenanceReportImages')
GROUP BY c.relname, c.relrowsecurity, c.relforcerowsecurity
ORDER BY c.relname;

-- Aggregate counts only; no descriptions, URLs, IDs, or person data.
SELECT 'MaintenanceReports' AS table_name, count(*)::bigint AS row_count
FROM public."MaintenanceReports"
UNION ALL
SELECT 'MaintenanceReportImages', count(*)::bigint
FROM public."MaintenanceReportImages"
ORDER BY table_name;

-- Required manual gate before any separately approved remediation:
-- 1) Verify every source-migration column, PK, FK, unique constraint and RLS flag.
-- 2) Decide whether the live CreatedAt default CURRENT_TIMESTAMP is intentional.
-- 3) If and only if index definitions are absent (not merely differently named),
--    review candidate forward-only repairs outside a transaction:
--    CREATE INDEX CONCURRENTLY IF NOT EXISTS IX_MaintenanceReports_ReportedBy
--        ON public."MaintenanceReports" ("ReportedBy");
--    CREATE INDEX CONCURRENTLY IF NOT EXISTS IX_MaintenanceReportImages_ReportId
--        ON public."MaintenanceReportImages" ("ReportId");
-- 4) Re-run this preflight and obtain separate approval before recording the
--    missing migration as reconciled. No history INSERT is provided here.
-- 5) If any expected schema/data check differs, STOP and prepare a reviewed
--    forward-only repair. Never run the historical migration Up/Down blindly.

ROLLBACK;
