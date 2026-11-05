-- READ-ONLY deployment postflight for 20261104032932_AddPropertyManagerAssignments.
-- Run only after an approved migration; this script makes no schema or row changes.
BEGIN TRANSACTION READ ONLY;

SELECT current_database() AS database_name, current_user AS database_role;

SELECT
    to_regclass('public."PropertyManagerAssignments"') IS NOT NULL AS assignment_table_present,
    to_regclass('public."PrepaidRentCreditApplications"') IS NULL AS prepaid_ledger_table_absent,
    EXISTS (
        SELECT 1
        FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20261104032932_AddPropertyManagerAssignments'
    ) AS migration_recorded;

SELECT array_agg(column_name ORDER BY ordinal_position) AS assignment_columns,
       COUNT(*) = 2 AS assignment_has_exactly_two_columns
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'PropertyManagerAssignments';

SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND ((table_name = 'Contracts' AND column_name IN (
          'LessorUserId', 'LessorNameSnapshot', 'LessorAddressSnapshot', 'PrepaidRentAmount'))
       OR (table_name = 'Invoices' AND column_name = 'PrepaidRentApplied'))
ORDER BY table_name, ordinal_position;

SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
      AND ((tablename = 'Invoices' AND indexname = 'IX_Invoices_ContractId')
        OR (tablename = 'PropertyManagerAssignments' AND indexname IN (
           'PK_PropertyManagerAssignments', 'IX_PropertyManagerAssignments_ManagerId')))
ORDER BY tablename, indexname;

SELECT conrelid::regclass AS table_name,
       conname,
       pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid = to_regclass('public."PropertyManagerAssignments"')
ORDER BY conname;

SELECT COUNT(*) AS assignment_rows
FROM "PropertyManagerAssignments";

SELECT c.relrowsecurity AS row_level_security_enabled,
       has_table_privilege('anon', c.oid, 'SELECT') AS anon_can_select,
       has_table_privilege('anon', c.oid, 'INSERT') AS anon_can_insert,
       has_table_privilege('authenticated', c.oid, 'SELECT') AS authenticated_can_select,
       has_table_privilege('authenticated', c.oid, 'INSERT') AS authenticated_can_insert,
       has_table_privilege('service_role', c.oid, 'SELECT') AS service_role_can_select
FROM pg_class AS c
WHERE c.oid = to_regclass('public."PropertyManagerAssignments"');

ROLLBACK;
