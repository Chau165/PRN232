-- Generated from EF Core migration 20261027054119_AddMaintenanceAssignmentNote.
-- Apply this script once, only after reviewing the target database and backup.
-- Do not run the ALTER TABLE separately from this migration script.
START TRANSACTION;

ALTER TABLE public."MaintenanceRequests" ADD "AssignmentNote" text;

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261027054119_AddMaintenanceAssignmentNote', '8.0.10');

COMMIT;
