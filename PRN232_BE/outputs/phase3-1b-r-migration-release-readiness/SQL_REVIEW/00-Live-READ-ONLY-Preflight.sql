-- REVIEW ONLY. Read-only live preflight; run with a dedicated read-only role when available.
-- No table data values, URLs, customer details, or secrets are selected.
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';

SELECT current_database() AS database_name,
       current_schema() AS current_schema,
       current_setting('server_version') AS server_version,
       current_setting('transaction_read_only') AS transaction_read_only,
       current_user AS effective_role,
       current_setting('TimeZone') AS timezone;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT table_name, column_name, data_type, is_nullable, column_default, ordinal_position
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages',
                     'Properties','InvoiceDetails','PropertyAmenities','PropertyServiceRates')
ORDER BY table_name, ordinal_position;

SELECT t.relname AS table_name,
       c.reltuples::bigint AS estimated_rows,
       c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS force_rls,
       (SELECT count(*) FROM pg_policies p
         WHERE p.schemaname='public' AND p.tablename=t.relname) AS policy_count
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
JOIN pg_stat_user_tables t ON t.relid=c.oid
WHERE n.nspname='public'
  AND t.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages',
                    'Properties','InvoiceDetails')
ORDER BY t.relname;

SELECT 'Properties' AS table_name, count(*) AS row_count FROM public."Properties"
UNION ALL SELECT 'InvoiceDetails', count(*) FROM public."InvoiceDetails"
UNION ALL SELECT 'MaintenanceLogs', count(*) FROM public."MaintenanceLogs"
UNION ALL SELECT 'MaintenanceReports', count(*) FROM public."MaintenanceReports"
UNION ALL SELECT 'MaintenanceReportImages', count(*) FROM public."MaintenanceReportImages"
ORDER BY table_name;

SELECT conrelid::regclass AS table_name,
       conname AS constraint_name,
       contype AS constraint_type,
       convalidated AS validated,
       condeferrable AS deferrable,
       pg_get_constraintdef(oid, true) AS definition
FROM pg_constraint
WHERE conrelid IN ('public."MaintenanceLogs"'::regclass,
                   'public."MaintenanceReports"'::regclass,
                   'public."MaintenanceReportImages"'::regclass)
ORDER BY table_name, constraint_name;

SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname='public'
  AND tablename IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages')
ORDER BY tablename, indexname;

SELECT extname, extversion, extnamespace::regnamespace AS extension_schema
FROM pg_extension
WHERE extname='btree_gist';

SELECT name, version, superuser, trusted, relocatable, schema
FROM pg_available_extension_versions
WHERE name='btree_gist'
ORDER BY version;

SELECT r.rolname,
       r.rolsuper,
       r.rolbypassrls,
       has_database_privilege(r.rolname, current_database(), 'CREATE') AS can_create_in_database
FROM pg_roles r
WHERE r.rolname IN (current_user,'anon','authenticated','service_role')
ORDER BY r.rolname;

SELECT to_regclass('public."PropertyAmenities"') IS NOT NULL AS property_amenities_exists,
       to_regclass('public."PropertyServiceRates"') IS NOT NULL AS property_service_rates_exists,
       EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='public' AND table_name='Properties' AND column_name='Description') AS property_description_exists,
       (SELECT count(*) FROM information_schema.columns
         WHERE table_schema='public' AND table_name='InvoiceDetails'
           AND column_name IN ('Quantity','UnitPrice','UnitCode','BillingBasis','ServiceId','RateId')) AS invoice_snapshot_column_count;

ROLLBACK;
