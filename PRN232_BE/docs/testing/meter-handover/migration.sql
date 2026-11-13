START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113132725_AddContractMeterHandoverBaseline') THEN
    ALTER TABLE "Contracts" ADD "InitialElectricReading" numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113132725_AddContractMeterHandoverBaseline') THEN
    ALTER TABLE "Contracts" ADD "InitialWaterReading" numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113132725_AddContractMeterHandoverBaseline') THEN
    ALTER TABLE "Contracts" ADD "MeterHandoverDate" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113132725_AddContractMeterHandoverBaseline') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113132725_AddContractMeterHandoverBaseline', '8.0.10');
    END IF;
END $EF$;
COMMIT;

