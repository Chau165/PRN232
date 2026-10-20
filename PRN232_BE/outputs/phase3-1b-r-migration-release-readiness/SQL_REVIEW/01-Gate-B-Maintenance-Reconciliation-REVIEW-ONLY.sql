-- REVIEW ONLY / DO NOT RUN ON LIVE.
-- Gate B candidate, based on the 2026-10-19 read-only snapshot. This version KEEPS
-- MaintenanceReports.CreatedAt DEFAULT CURRENT_TIMESTAMP as an intentional live extra.
-- It also preserves the two live MaintenanceLogs status checks and live MaintenanceLogs RLS.
-- Owner/DBA must approve these three live extras and this exact file hash before production use.
-- Execute with psql in autocommit mode. Do not use psql --single-transaction / -1:
-- CREATE INDEX CONCURRENTLY cannot run inside a transaction block.
-- Default behavior is fail-closed: pass -v phase31br_gate_b_approved=true only after
-- separate approval of this exact SHA-256 and a fresh read-only preflight.

\set ON_ERROR_STOP on
\if :{?phase31br_gate_b_approved}
  \if :phase31br_gate_b_approved
    \echo 'Gate B approval switch present; SQL preconditions still must match.'
  \else
    \echo 'BLOCKED: Gate B approval switch is false. No SQL changes were attempted.'
    DO $$ BEGIN RAISE EXCEPTION 'Gate B requires the explicit approval switch'; END $$;
  \endif
\else
  \echo 'BLOCKED: Gate B approval switch is missing. No SQL changes were attempted.'
  DO $$ BEGIN RAISE EXCEPTION 'Gate B requires the explicit approval switch'; END $$;
\endif

-- Read-only assertions. Any drift aborts before either concurrent index starts.
DO $gate_b_preflight$
DECLARE
    v_actual text[];
    v_default text;
BEGIN
    IF current_schema() <> 'public' THEN
        RAISE EXCEPTION 'Gate B requires public as current_schema; found %', current_schema();
    END IF;

    IF (SELECT count(*) FROM public."__EFMigrationsHistory") <> 4
       OR EXISTS (
           SELECT 1 FROM public."__EFMigrationsHistory"
           WHERE "MigrationId" NOT IN (
             '20261003142421_InitialCreate',
             '20261004073837_RenameKhuTroDichVuToPropertyService',
             '20261013140609_SeedRolesAndAuthSetup',
             '20261019034905_AddPropertyPrimaryImageUrl'
           ) OR "ProductVersion" <> '8.0.10'
       )
       OR EXISTS (
           SELECT 1 FROM public."__EFMigrationsHistory"
           WHERE "MigrationId"='20261013192052_ReconcileMaintenanceReportsAndLogNullability'
       ) THEN
        RAISE EXCEPTION 'Gate B migration history differs from the reviewed live snapshot';
    END IF;

    IF to_regclass('public."MaintenanceLogs"') IS NULL
       OR to_regclass('public."MaintenanceReports"') IS NULL
       OR to_regclass('public."MaintenanceReportImages"') IS NULL THEN
        RAISE EXCEPTION 'A required Maintenance table is missing';
    END IF;

    SELECT array_agg(format('%s|%s|%s|%s', a.attname,
                            format_type(a.atttypid,a.atttypmod),
                            a.attnotnull,
                            coalesce(nullif(a.attidentity,''),'-')) ORDER BY a.attnum)
      INTO v_actual
      FROM pg_attribute a
      JOIN pg_class c ON c.oid=a.attrelid
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='public' AND c.relname='MaintenanceLogs'
       AND a.attnum>0 AND NOT a.attisdropped;
    IF v_actual IS DISTINCT FROM ARRAY[
        'LogId|integer|t|d','RequestId|integer|t|-','OldStatus|text|f|-',
        'NewStatus|text|t|-','ChangedBy|integer|f|-','ChangedAt|timestamp with time zone|t|-'
    ]::text[] THEN
        RAISE EXCEPTION 'MaintenanceLogs columns differ from the reviewed post-Up shape';
    END IF;

    SELECT array_agg(format('%s|%s|%s|%s', a.attname,
                            format_type(a.atttypid,a.atttypmod),
                            a.attnotnull,
                            coalesce(nullif(a.attidentity,''),'-')) ORDER BY a.attnum)
      INTO v_actual
      FROM pg_attribute a
      JOIN pg_class c ON c.oid=a.attrelid
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='public' AND c.relname='MaintenanceReports'
       AND a.attnum>0 AND NOT a.attisdropped;
    IF v_actual IS DISTINCT FROM ARRAY[
        'ReportId|integer|t|d','RequestId|integer|t|-','ReportedBy|integer|t|-',
        'Description|text|t|-','CreatedAt|timestamp with time zone|t|-'
    ]::text[] THEN
        RAISE EXCEPTION 'MaintenanceReports columns differ from the reviewed post-Up shape';
    END IF;

    SELECT pg_get_expr(d.adbin,d.adrelid) INTO v_default
      FROM pg_attribute a
      JOIN pg_class c ON c.oid=a.attrelid
      JOIN pg_namespace n ON n.oid=c.relnamespace
      LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
     WHERE n.nspname='public' AND c.relname='MaintenanceReports' AND a.attname='CreatedAt';
    IF v_default IS DISTINCT FROM 'CURRENT_TIMESTAMP' THEN
        RAISE EXCEPTION 'CreatedAt default differs; this candidate only supports an approved KEEP CURRENT_TIMESTAMP decision';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_attribute a
        JOIN pg_class c ON c.oid=a.attrelid
        JOIN pg_namespace n ON n.oid=c.relnamespace
        LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
        WHERE n.nspname='public'
          AND c.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages')
          AND a.attnum>0 AND NOT a.attisdropped
          AND a.attgenerated<>''
    ) OR EXISTS (
        SELECT 1 FROM pg_attribute a
        JOIN pg_class c ON c.oid=a.attrelid
        JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
        WHERE n.nspname='public'
          AND c.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages')
          AND a.attnum>0 AND NOT a.attisdropped
          AND NOT (c.relname='MaintenanceReports' AND a.attname='CreatedAt'
                   AND pg_get_expr(d.adbin,d.adrelid)='CURRENT_TIMESTAMP')
    ) THEN
        RAISE EXCEPTION 'An unexpected generated column/default exists on Maintenance tables';
    END IF;

    SELECT array_agg(format('%s|%s|%s|%s', a.attname,
                            format_type(a.atttypid,a.atttypmod),
                            a.attnotnull,
                            coalesce(nullif(a.attidentity,''),'-')) ORDER BY a.attnum)
      INTO v_actual
      FROM pg_attribute a
      JOIN pg_class c ON c.oid=a.attrelid
      JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname='public' AND c.relname='MaintenanceReportImages'
       AND a.attnum>0 AND NOT a.attisdropped;
    IF v_actual IS DISTINCT FROM ARRAY[
        'ImageId|integer|t|d','ReportId|integer|t|-','ImageUrl|text|t|-'
    ]::text[] THEN
        RAISE EXCEPTION 'MaintenanceReportImages columns differ from the reviewed post-Up shape';
    END IF;

    IF (SELECT count(*) FROM pg_constraint
         WHERE conrelid IN ('public."MaintenanceLogs"'::regclass,
                            'public."MaintenanceReports"'::regclass,
                            'public."MaintenanceReportImages"'::regclass)) <> 10
       OR EXISTS (
           SELECT 1 FROM pg_constraint
           WHERE conrelid IN ('public."MaintenanceLogs"'::regclass,
                              'public."MaintenanceReports"'::regclass,
                              'public."MaintenanceReportImages"'::regclass)
              AND (NOT convalidated OR condeferrable OR condeferred)
       ) THEN
        RAISE EXCEPTION 'Maintenance constraint count/validation differs from reviewed source';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceLogs"'::regclass AND conname='PK_MaintenanceLogs' AND pg_get_constraintdef(oid,true)='PRIMARY KEY ("LogId")')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceLogs"'::regclass AND conname='FK_MaintenanceLogs_MaintenanceRequests_RequestId' AND pg_get_constraintdef(oid,true)='FOREIGN KEY ("RequestId") REFERENCES "MaintenanceRequests"("RequestId") ON DELETE CASCADE')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceLogs"'::regclass AND conname='FK_MaintenanceLogs_Users_ChangedBy' AND pg_get_constraintdef(oid,true)='FOREIGN KEY ("ChangedBy") REFERENCES "Users"("UserId") ON DELETE RESTRICT')
        OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceLogs"'::regclass AND conname='CK_MaintenanceLogs_OldStatus' AND pg_get_constraintdef(oid,true)='CHECK ("OldStatus" = ANY (ARRAY[''Pending''::text, ''Approved''::text, ''Completed''::text]))')
        OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceLogs"'::regclass AND conname='CK_MaintenanceLogs_NewStatus' AND pg_get_constraintdef(oid,true)='CHECK ("NewStatus" = ANY (ARRAY[''Pending''::text, ''Approved''::text, ''Completed''::text]))')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceReports"'::regclass AND conname='PK_MaintenanceReports' AND pg_get_constraintdef(oid,true)='PRIMARY KEY ("ReportId")')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceReports"'::regclass AND conname='FK_MaintenanceReports_MaintenanceRequests_RequestId' AND pg_get_constraintdef(oid,true)='FOREIGN KEY ("RequestId") REFERENCES "MaintenanceRequests"("RequestId") ON DELETE CASCADE')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceReports"'::regclass AND conname='FK_MaintenanceReports_Users_ReportedBy' AND pg_get_constraintdef(oid,true)='FOREIGN KEY ("ReportedBy") REFERENCES "Users"("UserId") ON DELETE RESTRICT')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceReportImages"'::regclass AND conname='PK_MaintenanceReportImages' AND pg_get_constraintdef(oid,true)='PRIMARY KEY ("ImageId")')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='public."MaintenanceReportImages"'::regclass AND conname='FK_MaintenanceReportImages_MaintenanceReports_ReportId' AND pg_get_constraintdef(oid,true)='FOREIGN KEY ("ReportId") REFERENCES "MaintenanceReports"("ReportId") ON DELETE CASCADE') THEN
        RAISE EXCEPTION 'Maintenance PK/FK/check constraints differ from reviewed source';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
        WHERE n.nspname='public' AND t.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages','__EFMigrationsHistory')
          AND t.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user)
    ) THEN
        RAISE EXCEPTION 'Current role does not own every required Maintenance/history table';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND c.relname IN ('MaintenanceReports','MaintenanceReportImages')
          AND (NOT c.relrowsecurity OR c.relforcerowsecurity)
    ) OR EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname='public' AND tablename IN ('MaintenanceReports','MaintenanceReportImages')
    ) THEN
        RAISE EXCEPTION 'Maintenance RLS flags/policies differ from reviewed source';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='public' AND c.relname='MaintenanceLogs'
          AND c.relrowsecurity AND NOT c.relforcerowsecurity
    ) OR EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname='public' AND tablename='MaintenanceLogs'
    ) THEN
        RAISE EXCEPTION 'MaintenanceLogs live RLS state differs from the reviewed snapshot';
    END IF;

    IF pg_get_serial_sequence('public."MaintenanceReports"','ReportId') IS NULL
       OR pg_get_serial_sequence('public."MaintenanceReportImages"','ImageId') IS NULL THEN
        RAISE EXCEPTION 'Maintenance identity sequence ownership is not intact';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
        JOIN pg_index ix ON ix.indrelid=t.oid JOIN pg_class i ON i.oid=ix.indexrelid
        WHERE n.nspname='public' AND t.relname IN ('MaintenanceLogs','MaintenanceReports','MaintenanceReportImages')
          AND i.relname NOT IN ('PK_MaintenanceLogs','IX_MaintenanceLogs_ChangedBy','IX_MaintenanceLogs_RequestId','PK_MaintenanceReports','IX_MaintenanceReports_RequestId','IX_MaintenanceReports_ReportedBy','PK_MaintenanceReportImages','IX_MaintenanceReportImages_ReportId')
    ) THEN
        RAISE EXCEPTION 'Unexpected Maintenance index exists; review before adding indexes';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='PK_MaintenanceLogs' AND indexdef='CREATE UNIQUE INDEX "PK_MaintenanceLogs" ON public."MaintenanceLogs" USING btree ("LogId")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceLogs_ChangedBy' AND indexdef='CREATE INDEX "IX_MaintenanceLogs_ChangedBy" ON public."MaintenanceLogs" USING btree ("ChangedBy")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceLogs_RequestId' AND indexdef='CREATE INDEX "IX_MaintenanceLogs_RequestId" ON public."MaintenanceLogs" USING btree ("RequestId")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='PK_MaintenanceReports' AND indexdef='CREATE UNIQUE INDEX "PK_MaintenanceReports" ON public."MaintenanceReports" USING btree ("ReportId")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceReports_RequestId' AND indexdef='CREATE UNIQUE INDEX "IX_MaintenanceReports_RequestId" ON public."MaintenanceReports" USING btree ("RequestId")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='PK_MaintenanceReportImages' AND indexdef='CREATE UNIQUE INDEX "PK_MaintenanceReportImages" ON public."MaintenanceReportImages" USING btree ("ImageId")') THEN
        RAISE EXCEPTION 'Maintenance PK/unique index definitions differ from reviewed source';
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
        JOIN pg_index ix ON ix.indrelid=t.oid JOIN pg_class i ON i.oid=ix.indexrelid
        JOIN pg_am am ON am.oid=i.relam
        JOIN pg_attribute a ON a.attrelid=t.oid AND a.attname='ReportedBy'
        WHERE n.nspname='public' AND t.relname='MaintenanceReports'
          AND ((i.relname='IX_MaintenanceReports_ReportedBy' AND NOT (ix.indisvalid AND ix.indisready AND NOT ix.indisunique AND NOT ix.indisprimary AND ix.indnatts=1 AND ix.indnkeyatts=1 AND ix.indkey[0]=a.attnum AND ix.indpred IS NULL AND ix.indexprs IS NULL AND am.amname='btree'))
            OR (i.relname<>'IX_MaintenanceReports_ReportedBy' AND ix.indisvalid AND ix.indisready AND NOT ix.indisunique AND NOT ix.indisprimary AND ix.indnatts=1 AND ix.indnkeyatts=1 AND ix.indkey[0]=a.attnum AND ix.indpred IS NULL AND ix.indexprs IS NULL AND am.amname='btree'))
    ) OR EXISTS (
        SELECT 1 FROM pg_class t JOIN pg_namespace n ON n.oid=t.relnamespace
        JOIN pg_index ix ON ix.indrelid=t.oid JOIN pg_class i ON i.oid=ix.indexrelid
        JOIN pg_am am ON am.oid=i.relam
        JOIN pg_attribute a ON a.attrelid=t.oid AND a.attname='ReportId'
        WHERE n.nspname='public' AND t.relname='MaintenanceReportImages'
          AND ((i.relname='IX_MaintenanceReportImages_ReportId' AND NOT (ix.indisvalid AND ix.indisready AND NOT ix.indisunique AND NOT ix.indisprimary AND ix.indnatts=1 AND ix.indnkeyatts=1 AND ix.indkey[0]=a.attnum AND ix.indpred IS NULL AND ix.indexprs IS NULL AND am.amname='btree'))
            OR (i.relname<>'IX_MaintenanceReportImages_ReportId' AND ix.indisvalid AND ix.indisready AND NOT ix.indisunique AND NOT ix.indisprimary AND ix.indnatts=1 AND ix.indnkeyatts=1 AND ix.indkey[0]=a.attnum AND ix.indpred IS NULL AND ix.indexprs IS NULL AND am.amname='btree'))
    ) THEN
        RAISE EXCEPTION 'Expected secondary index has a conflicting or alternate exact definition; review manually';
    END IF;
END
$gate_b_preflight$;

-- Each statement commits independently. A failed CONCURRENTLY build may leave an
-- INVALID index; inspect pg_index.indisvalid and obtain separate cleanup approval.
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_MaintenanceReports_ReportedBy"
    ON public."MaintenanceReports" USING btree ("ReportedBy");
CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_MaintenanceReportImages_ReportId"
    ON public."MaintenanceReportImages" USING btree ("ReportId");

-- Only the exact expected indexes may proceed to the history transaction.
DO $gate_b_index_postcheck$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceReports_ReportedBy' AND indexdef='CREATE INDEX "IX_MaintenanceReports_ReportedBy" ON public."MaintenanceReports" USING btree ("ReportedBy")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceReportImages_ReportId' AND indexdef='CREATE INDEX "IX_MaintenanceReportImages_ReportId" ON public."MaintenanceReportImages" USING btree ("ReportId")')
       OR EXISTS (
          SELECT 1 FROM pg_index ix JOIN pg_class i ON i.oid=ix.indexrelid
          JOIN pg_namespace n ON n.oid=i.relnamespace
          WHERE n.nspname='public' AND i.relname IN ('IX_MaintenanceReports_ReportedBy','IX_MaintenanceReportImages_ReportId')
            AND (NOT ix.indisvalid OR NOT ix.indisready)
       ) THEN
        RAISE EXCEPTION 'Gate B concurrent index postcondition failed; do not write migration history';
    END IF;
END
$gate_b_index_postcheck$;

-- History is recorded only after every source postcondition above is true.
-- CreatedAt's extra default is retained only under the separately approved choice.
BEGIN;
SET LOCAL statement_timeout = '15000ms';
DO $gate_b_history_guard$
BEGIN
    IF (SELECT count(*) FROM public."__EFMigrationsHistory") <> 4
       OR NOT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId"='20261019034905_AddPropertyPrimaryImageUrl' AND "ProductVersion"='8.0.10')
       OR EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId"='20261013192052_ReconcileMaintenanceReportsAndLogNullability')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceReports_ReportedBy' AND indexdef='CREATE INDEX "IX_MaintenanceReports_ReportedBy" ON public."MaintenanceReports" USING btree ("ReportedBy")')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='public' AND indexname='IX_MaintenanceReportImages_ReportId' AND indexdef='CREATE INDEX "IX_MaintenanceReportImages_ReportId" ON public."MaintenanceReportImages" USING btree ("ReportId")')
       OR (SELECT pg_get_expr(d.adbin,d.adrelid) FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum WHERE n.nspname='public' AND c.relname='MaintenanceReports' AND a.attname='CreatedAt') IS DISTINCT FROM 'CURRENT_TIMESTAMP'
       OR (SELECT attnotnull FROM pg_attribute WHERE attrelid='public."MaintenanceLogs"'::regclass AND attname='OldStatus') THEN
        RAISE EXCEPTION 'Gate B final guard failed; __EFMigrationsHistory remains unchanged';
    END IF;
END
$gate_b_history_guard$;
INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261013192052_ReconcileMaintenanceReportsAndLogNullability','8.0.10');
COMMIT;
