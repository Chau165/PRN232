START TRANSACTION;

-- Operator-only rollback: never discard a genuine handover, including partial records.
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM "Contracts" WHERE "InitialElectricReading" IS NOT NULL
        OR "InitialWaterReading" IS NOT NULL OR "MeterHandoverDate" IS NOT NULL) THEN
        RAISE EXCEPTION 'Keep baseline columns: handover data exists. Roll back the application instead.';
    END IF;
END $guard$;

ALTER TABLE "Contracts" DROP COLUMN "InitialElectricReading";

ALTER TABLE "Contracts" DROP COLUMN "InitialWaterReading";

ALTER TABLE "Contracts" DROP COLUMN "MeterHandoverDate";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261113132725_AddContractMeterHandoverBaseline';

COMMIT;

