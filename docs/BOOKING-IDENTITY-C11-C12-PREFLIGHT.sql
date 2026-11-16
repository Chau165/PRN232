-- Read-only. Run in Supabase SQL Editor with the project owner.
-- Does not select names, CCCD, image paths, keys, bank accounts or signed URLs.
BEGIN TRANSACTION READ ONLY;
SELECT "MigrationId" FROM public."__EFMigrationsHistory"
WHERE "MigrationId" IN (
 '20261116005532_AddBookingIdentityPreparation',
 '20261116022416_AddBookingApplicationReviewWorkflow',
 '20261116030926_AddContractIdentitySnapshotsAndCleanup'
) ORDER BY "MigrationId";

WITH required(name) AS (VALUES
 ('BookingApplications'),('BookingApplicationPeople'),('BookingIdentityImages'),
 ('BookingApplicationReviews'),('ContractIdentitySnapshots'),
 ('BookingIdentityCleanupTasks'),('BookingIdentityRetentionAudits'))
SELECT r.name AS table_name, c.oid IS NOT NULL AS table_exists,
       c.relrowsecurity AS rls_enabled
FROM required r LEFT JOIN pg_class c ON c.oid=to_regclass(format('public.%I',r.name))
ORDER BY r.name;

-- Expected: no direct SELECT permission for anon/authenticated on these private tables.
WITH required(name) AS (VALUES
 ('BookingApplications'),('BookingApplicationPeople'),('BookingIdentityImages'),
 ('BookingApplicationReviews'),('ContractIdentitySnapshots'),
 ('BookingIdentityCleanupTasks'),('BookingIdentityRetentionAudits'))
SELECT role.rolname, r.name AS table_name,
       has_table_privilege(role.oid,to_regclass(format('public.%I',r.name)),'SELECT') AS can_select
FROM pg_roles role CROSS JOIN required r
WHERE role.rolname IN ('anon','authenticated') ORDER BY role.rolname,r.name;
COMMIT;
