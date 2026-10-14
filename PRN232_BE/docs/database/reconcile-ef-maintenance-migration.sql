-- REVIEW ONLY. This script was prepared but has NOT been executed.
-- It records the EF migration only after the live schema exactly matches its target.
-- Audit on 2026-10-14 found IX_MaintenanceReportImages_ReportId and
-- IX_MaintenanceReports_ReportedBy missing, so this script must currently stop.

DO $reconcile$
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'EF migrations history table is missing.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261013192052_ReconcileMaintenanceReportsAndLogNullability'
          AND "ProductVersion" <> '8.0.10'
    ) THEN
        RAISE EXCEPTION 'The target migration already exists with an unexpected ProductVersion.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261003142421_InitialCreate' AND "ProductVersion" = '8.0.10'
    ) OR NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261004073837_RenameKhuTroDichVuToPropertyService' AND "ProductVersion" = '8.0.10'
    ) OR NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261013140609_SeedRolesAndAuthSetup' AND "ProductVersion" = '8.0.10'
    ) THEN
        RAISE EXCEPTION 'The expected prior EF migration history is incomplete or has a different ProductVersion.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261013192052_ReconcileMaintenanceReportsAndLogNullability'
    ) AND (
        SELECT max("MigrationId") FROM public."__EFMigrationsHistory"
    ) <> '20261013140609_SeedRolesAndAuthSetup' THEN
        RAISE EXCEPTION 'Unexpected migration history follows or replaces the expected predecessor.';
    END IF;

    IF to_regclass('public."MaintenanceLogs"') IS NULL
       OR (SELECT count(*) FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'MaintenanceLogs') <> 6
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'MaintenanceLogs'
             AND column_name = 'OldStatus' AND data_type = 'text' AND is_nullable = 'YES'
       ) THEN
        RAISE EXCEPTION 'MaintenanceLogs does not match the migration target.';
    END IF;

    IF to_regclass('public."MaintenanceReports"') IS NULL
       OR (SELECT count(*) FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'MaintenanceReports') <> 5
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReports' AND column_name = 'ReportId'
             AND data_type = 'integer' AND is_nullable = 'NO'
             AND is_identity = 'YES' AND identity_generation = 'BY DEFAULT'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReports' AND column_name = 'RequestId'
             AND data_type = 'integer' AND is_nullable = 'NO' AND is_identity = 'NO'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReports' AND column_name = 'ReportedBy'
             AND data_type = 'integer' AND is_nullable = 'NO' AND is_identity = 'NO'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReports' AND column_name = 'Description'
             AND data_type = 'text' AND is_nullable = 'NO' AND is_identity = 'NO'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReports' AND column_name = 'CreatedAt'
             AND data_type = 'timestamp with time zone' AND is_nullable = 'NO' AND is_identity = 'NO'
       ) THEN
        RAISE EXCEPTION 'MaintenanceReports columns or identity settings do not match the migration target.';
    END IF;

    IF to_regclass('public."MaintenanceReportImages"') IS NULL
       OR (SELECT count(*) FROM information_schema.columns
           WHERE table_schema = 'public' AND table_name = 'MaintenanceReportImages') <> 3
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReportImages' AND column_name = 'ImageId'
             AND data_type = 'integer' AND is_nullable = 'NO'
             AND is_identity = 'YES' AND identity_generation = 'BY DEFAULT'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReportImages' AND column_name = 'ReportId'
             AND data_type = 'integer' AND is_nullable = 'NO' AND is_identity = 'NO'
       )
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
             AND table_name = 'MaintenanceReportImages' AND column_name = 'ImageUrl'
             AND data_type = 'text' AND is_nullable = 'NO' AND is_identity = 'NO'
       ) THEN
        RAISE EXCEPTION 'MaintenanceReportImages columns or identity settings do not match the migration target.';
    END IF;

    IF (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname IN ('MaintenanceReports', 'MaintenanceReportImages')
          AND c.relkind = 'r' AND c.relrowsecurity AND NOT c.relforcerowsecurity) <> 2
       OR EXISTS (
           SELECT 1 FROM pg_policies
           WHERE schemaname = 'public'
             AND tablename IN ('MaintenanceReports', 'MaintenanceReportImages')
       ) THEN
        RAISE EXCEPTION 'Maintenance report RLS state or policy set does not match the migration target.';
    END IF;

    IF (SELECT count(*) FROM pg_constraint
        WHERE conrelid IN (to_regclass('public."MaintenanceReports"'),
                           to_regclass('public."MaintenanceReportImages"'))) <> 5
       OR NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           WHERE c.conrelid = to_regclass('public."MaintenanceReports"')
             AND c.conname = 'PK_MaintenanceReports' AND c.contype = 'p' AND c.convalidated
             AND c.conkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                   WHERE a.attrelid = c.conrelid AND a.attname = 'ReportId'
                                     AND NOT a.attisdropped)]::smallint[]
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           WHERE c.conrelid = to_regclass('public."MaintenanceReportImages"')
             AND c.conname = 'PK_MaintenanceReportImages' AND c.contype = 'p' AND c.convalidated
             AND c.conkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                   WHERE a.attrelid = c.conrelid AND a.attname = 'ImageId'
                                     AND NOT a.attisdropped)]::smallint[]
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           WHERE c.conrelid = to_regclass('public."MaintenanceReports"')
             AND c.conname = 'FK_MaintenanceReports_MaintenanceRequests_RequestId'
             AND c.contype = 'f' AND c.convalidated AND c.confdeltype = 'c' AND c.confupdtype = 'a'
             AND c.conkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                   WHERE a.attrelid = c.conrelid AND a.attname = 'RequestId'
                                     AND NOT a.attisdropped)]::smallint[]
             AND c.confrelid = to_regclass('public."MaintenanceRequests"')
             AND c.confkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                    WHERE a.attrelid = c.confrelid AND a.attname = 'RequestId'
                                      AND NOT a.attisdropped)]::smallint[]
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           WHERE c.conrelid = to_regclass('public."MaintenanceReports"')
             AND c.conname = 'FK_MaintenanceReports_Users_ReportedBy'
             AND c.contype = 'f' AND c.convalidated AND c.confdeltype = 'r' AND c.confupdtype = 'a'
             AND c.conkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                   WHERE a.attrelid = c.conrelid AND a.attname = 'ReportedBy'
                                     AND NOT a.attisdropped)]::smallint[]
             AND c.confrelid = to_regclass('public."Users"')
             AND c.confkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                    WHERE a.attrelid = c.confrelid AND a.attname = 'UserId'
                                      AND NOT a.attisdropped)]::smallint[]
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_constraint c
           WHERE c.conrelid = to_regclass('public."MaintenanceReportImages"')
             AND c.conname = 'FK_MaintenanceReportImages_MaintenanceReports_ReportId'
             AND c.contype = 'f' AND c.convalidated AND c.confdeltype = 'c' AND c.confupdtype = 'a'
             AND c.conkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                   WHERE a.attrelid = c.conrelid AND a.attname = 'ReportId'
                                     AND NOT a.attisdropped)]::smallint[]
             AND c.confrelid = to_regclass('public."MaintenanceReports"')
             AND c.confkey = ARRAY[(SELECT a.attnum::smallint FROM pg_attribute a
                                    WHERE a.attrelid = c.confrelid AND a.attname = 'ReportId'
                                      AND NOT a.attisdropped)]::smallint[]
       ) THEN
        RAISE EXCEPTION 'Maintenance report primary/foreign keys do not match the migration target.';
    END IF;

    IF (SELECT count(*) FROM pg_index i
        JOIN pg_class t ON t.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname IN ('MaintenanceReports', 'MaintenanceReportImages')) <> 5
       OR NOT EXISTS (
           SELECT 1 FROM pg_index i JOIN pg_class t ON t.oid = i.indrelid
             JOIN pg_class ix ON ix.oid = i.indexrelid JOIN pg_namespace n ON n.oid = t.relnamespace
           WHERE n.nspname = 'public' AND t.relname = 'MaintenanceReports'
             AND ix.relname = 'IX_MaintenanceReports_RequestId'
             AND i.indisunique AND i.indisvalid AND i.indisready AND i.indpred IS NULL
             AND i.indnkeyatts = 1 AND i.indnatts = 1
             AND pg_get_indexdef(i.indexrelid, 1, true) = '"RequestId"'
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_index i JOIN pg_class t ON t.oid = i.indrelid
             JOIN pg_class ix ON ix.oid = i.indexrelid JOIN pg_namespace n ON n.oid = t.relnamespace
           WHERE n.nspname = 'public' AND t.relname = 'MaintenanceReports'
             AND ix.relname = 'IX_MaintenanceReports_ReportedBy'
             AND NOT i.indisunique AND i.indisvalid AND i.indisready AND i.indpred IS NULL
             AND i.indnkeyatts = 1 AND i.indnatts = 1
             AND pg_get_indexdef(i.indexrelid, 1, true) = '"ReportedBy"'
       )
       OR NOT EXISTS (
           SELECT 1 FROM pg_index i JOIN pg_class t ON t.oid = i.indrelid
             JOIN pg_class ix ON ix.oid = i.indexrelid JOIN pg_namespace n ON n.oid = t.relnamespace
           WHERE n.nspname = 'public' AND t.relname = 'MaintenanceReportImages'
             AND ix.relname = 'IX_MaintenanceReportImages_ReportId'
             AND NOT i.indisunique AND i.indisvalid AND i.indisready AND i.indpred IS NULL
             AND i.indnkeyatts = 1 AND i.indnatts = 1
             AND pg_get_indexdef(i.indexrelid, 1, true) = '"ReportId"'
       ) THEN
        RAISE EXCEPTION 'Maintenance report indexes do not match the migration target.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory"
        WHERE "MigrationId" = '20261013192052_ReconcileMaintenanceReportsAndLogNullability'
    ) THEN
        INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        VALUES ('20261013192052_ReconcileMaintenanceReportsAndLogNullability', '8.0.10');
    END IF;
END;
$reconcile$;
