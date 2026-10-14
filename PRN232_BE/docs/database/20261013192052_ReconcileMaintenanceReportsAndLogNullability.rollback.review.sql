-- EF-generated rollback review script for 20261013192052_ReconcileMaintenanceReportsAndLogNullability.
-- NOT EXECUTED. The NULL guard runs first; a successful rollback drops both report tables and their data.

START TRANSACTION;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "MaintenanceLogs" WHERE "OldStatus" IS NULL) THEN
        RAISE EXCEPTION 'Cannot revert this migration while MaintenanceLogs contains NULL OldStatus values.';
    END IF;
END;
$$;

DROP TABLE "MaintenanceReportImages";

DROP TABLE "MaintenanceReports";

ALTER TABLE "MaintenanceLogs" ALTER COLUMN "OldStatus" SET NOT NULL;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261013192052_ReconcileMaintenanceReportsAndLogNullability';

COMMIT;

