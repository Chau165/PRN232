-- READ-ONLY deployment preflight for 20261104032932_AddPropertyManagerAssignments.
-- Run against the intended database before applying the reviewed migration.
BEGIN TRANSACTION READ ONLY;

SELECT current_database() AS database_name, current_user AS database_role;

SELECT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20261103180414_AddViewingAssignmentsAndNotificationLinks'
) AS prerequisite_migration_applied;

SELECT
    to_regclass('public."PropertyManagerAssignments"') IS NULL AS assignment_table_not_yet_present,
    to_regclass('public."PrepaidRentCreditApplications"') IS NULL AS prepaid_ledger_table_not_yet_present,
    NOT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20261104032932_AddPropertyManagerAssignments'
    ) AS assignment_migration_not_applied,
    EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND tablename = 'Invoices'
          AND indexname = 'IX_Invoices_ContractId'
    ) AS legacy_invoice_index_remains_untouched;

SELECT table_name, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('Properties', 'Users')
ORDER BY table_name, ordinal_position;

SELECT table_name, constraint_name, constraint_type
FROM information_schema.table_constraints
WHERE table_schema = 'public'
  AND table_name IN ('Properties', 'Users')
  AND constraint_type = 'PRIMARY KEY'
ORDER BY table_name;

-- Ownership is deliberately not backfilled into Manager assignments.
-- Review non-Admin property owners before planning any manual assignment work.
SELECT property."PropertyId", property."OwnerId", role."RoleName" AS owner_role
FROM "Properties" AS property
LEFT JOIN "Users" AS owner ON owner."UserId" = property."OwnerId"
LEFT JOIN "Roles" AS role ON role."RoleId" = owner."RoleId"
WHERE role."RoleName" IS DISTINCT FROM 'Admin'
ORDER BY property."PropertyId";

ROLLBACK;
