-- REVIEW ONLY. Read-only postflight after approved Gate B/C/D/E execution.
-- Run immediately after deployment while writers are paused; compare counts to saved preflight.
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';

SELECT current_database() AS database_name,
       current_setting('server_version') AS server_version,
       current_setting('transaction_read_only') AS transaction_read_only,
       current_timestamp AS observed_at_utc;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT 'Properties' AS table_name, count(*) AS row_count FROM public."Properties"
UNION ALL SELECT 'InvoiceDetails', count(*) FROM public."InvoiceDetails"
UNION ALL SELECT 'MaintenanceLogs', count(*) FROM public."MaintenanceLogs"
UNION ALL SELECT 'MaintenanceReports', count(*) FROM public."MaintenanceReports"
UNION ALL SELECT 'MaintenanceReportImages', count(*) FROM public."MaintenanceReportImages"
UNION ALL SELECT 'PropertyAmenities', count(*) FROM public."PropertyAmenities"
UNION ALL SELECT 'PropertyServiceRates', count(*) FROM public."PropertyServiceRates"
ORDER BY table_name;

SELECT count(*) AS properties,
       count(*) FILTER (WHERE "Description" IS NULL) AS description_null_rows
FROM public."Properties";

SELECT count(*) AS invoice_details,
       count(*) FILTER (WHERE "Quantity" IS NULL
                        AND "UnitPrice" IS NULL
                        AND "UnitCode" IS NULL
                        AND "BillingBasis" IS NULL
                        AND "ServiceId" IS NULL
                        AND "RateId" IS NULL) AS rows_with_all_snapshot_fields_null
FROM public."InvoiceDetails";

SELECT count(*) FILTER (WHERE "OldStatus" IS NULL) AS maintenance_logs_with_null_old_status
FROM public."MaintenanceLogs";

SELECT pg_get_expr(d.adbin,d.adrelid) AS created_at_default
FROM pg_attribute a
JOIN pg_class c ON c.oid=a.attrelid
JOIN pg_namespace n ON n.oid=c.relnamespace
LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
WHERE n.nspname='public' AND c.relname='MaintenanceReports' AND a.attname='CreatedAt';

SELECT extname, extversion, extnamespace::regnamespace AS extension_schema
FROM pg_extension
WHERE extname='btree_gist';

SELECT c.relname AS table_name, c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS force_rls,
       (SELECT count(*) FROM pg_policies p
        WHERE p.schemaname='public' AND p.tablename=c.relname) AS policy_count
FROM pg_class c
JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE n.nspname='public'
  AND c.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages',
                    'PropertyAmenities','PropertyServiceRates')
ORDER BY c.relname;

SELECT conrelid::regclass AS table_name,
       conname AS constraint_name,
       contype AS constraint_type,
       convalidated AS validated,
       pg_get_constraintdef(oid,true) AS definition
FROM pg_constraint
WHERE conrelid IN ('public."MaintenanceLogs"'::regclass,
                   'public."MaintenanceReports"'::regclass,
                   'public."MaintenanceReportImages"'::regclass,
                   'public."PropertyAmenities"'::regclass,
                   'public."PropertyServiceRates"'::regclass,
                   'public."InvoiceDetails"'::regclass)
ORDER BY table_name,constraint_name;

SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname='public'
  AND tablename IN ('MaintenanceReports','MaintenanceReportImages',
                    'PropertyAmenities','PropertyServiceRates','InvoiceDetails')
ORDER BY tablename,indexname;

SELECT i.relname AS index_name, ix.indisvalid AS valid, ix.indisready AS ready
FROM pg_index ix
JOIN pg_class i ON i.oid=ix.indexrelid
JOIN pg_namespace n ON n.oid=i.relnamespace
WHERE n.nspname='public'
  AND i.relname IN ('IX_MaintenanceReports_ReportedBy',
                    'IX_MaintenanceReportImages_ReportId',
                    'EX_PropertyServiceRates_NoOverlappingPeriods')
ORDER BY i.relname;

ROLLBACK;
