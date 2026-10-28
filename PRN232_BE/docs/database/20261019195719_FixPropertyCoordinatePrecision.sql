-- Generated from EF Core migration 20261019195719_FixPropertyCoordinatePrecision.
-- ALREADY APPLIED on the shared Supabase database on 2026-10-19; do not run it there again.
-- The migration file was missing from source and was restored on 2026-10-28 with the same
-- MigrationId, so Supabase's history and the code agree. Kept for reference and fresh databases.
START TRANSACTION;

ALTER TABLE public."Properties" ALTER COLUMN "Longitude" TYPE numeric(9,6);

ALTER TABLE public."Properties" ALTER COLUMN "Latitude" TYPE numeric(9,6);

INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261019195719_FixPropertyCoordinatePrecision', '8.0.10');

COMMIT;
