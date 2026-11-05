-- READ ONLY preflight for PRN232 Viewing Schedule assignment migration.
-- No schema or row changes are made. Verify target database identity separately
-- using the approved project connection; never put credentials in this file.

BEGIN TRANSACTION READ ONLY;

SELECT current_database() AS database_name, current_schema() AS current_schema;

SELECT table_name, column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('ViewingSchedules', 'Notifications')
ORDER BY table_name, ordinal_position;

SELECT 'constraint' AS object_type, c.conname AS object_name, pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c
WHERE c.conrelid = to_regclass('public."ViewingSchedules"')
UNION ALL
SELECT 'index', i.indexname, i.indexdef
FROM pg_indexes i
WHERE i.schemaname = 'public' AND i.tablename = 'ViewingSchedules'
ORDER BY object_type, object_name;

SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT EXISTS (
    SELECT 1
    FROM public."__EFMigrationsHistory"
    WHERE "MigrationId" = '20261103180414_AddViewingAssignmentsAndNotificationLinks'
) AS viewing_assignment_migration_applied;

SELECT "Status", count(*) AS viewing_count
FROM public."ViewingSchedules"
GROUP BY "Status"
ORDER BY "Status";

SELECT count(*) AS viewing_schedule_rows FROM public."ViewingSchedules";
SELECT count(*) AS notification_rows FROM public."Notifications";

ROLLBACK;
