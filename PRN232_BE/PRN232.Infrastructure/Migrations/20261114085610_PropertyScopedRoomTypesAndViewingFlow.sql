START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" DROP CONSTRAINT "FK_ViewingSchedules_Rooms_RoomId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    DROP INDEX "IX_ViewingSchedules_RoomId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    DROP INDEX "IX_Rooms_PropertyId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ALTER COLUMN "RoomId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ADD "PropertyId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ADD "RoomTypeId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Bookings" ADD "AgreedMonthlyRent" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Bookings" ADD "ViewingId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Rooms" ADD CONSTRAINT "AK_Rooms_PropertyId_RoomTypeId_RoomId" UNIQUE ("PropertyId", "RoomTypeId", "RoomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE TABLE "PropertyRoomTypes" (
        "PropertyId" integer NOT NULL,
        "RoomTypeId" integer NOT NULL,
        "MonthlyRent" numeric(18,2) NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_PropertyRoomTypes" PRIMARY KEY ("PropertyId", "RoomTypeId"),
        CONSTRAINT "CK_PropertyRoomTypes_MonthlyRent_Positive" CHECK ("MonthlyRent" > 0),
        CONSTRAINT "FK_PropertyRoomTypes_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("PropertyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PropertyRoomTypes_RoomTypes_RoomTypeId" FOREIGN KEY ("RoomTypeId") REFERENCES "RoomTypes" ("RoomTypeId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    INSERT INTO public."PropertyRoomTypes" ("PropertyId", "RoomTypeId", "MonthlyRent", "IsActive")
    SELECT DISTINCT
        room."PropertyId",
        room."RoomTypeId",
        roomType."Price",
        roomType."Price" >= 10000
    FROM public."Rooms" AS room
    INNER JOIN public."RoomTypes" AS roomType
        ON roomType."RoomTypeId" = room."RoomTypeId"
    ON CONFLICT ("PropertyId", "RoomTypeId") DO NOTHING;

    UPDATE public."ViewingSchedules" AS viewing
    SET "PropertyId" = room."PropertyId",
        "RoomTypeId" = room."RoomTypeId"
    FROM public."Rooms" AS room
    WHERE room."RoomId" = viewing."RoomId";

    DO $migration$
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM public."ViewingSchedules"
            WHERE "PropertyId" IS NULL OR "RoomTypeId" IS NULL
        ) THEN
            RAISE EXCEPTION 'ViewingSchedules backfill did not resolve every existing RoomId';
        END IF;

        ALTER TABLE public."PropertyRoomTypes" ENABLE ROW LEVEL SECURITY;

        EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM PUBLIC';
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
            EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM anon';
        END IF;
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
            EXECUTE 'REVOKE ALL ON TABLE public."PropertyRoomTypes" FROM authenticated';
        END IF;
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
            EXECUTE 'GRANT SELECT, INSERT, UPDATE ON TABLE public."PropertyRoomTypes" TO service_role';
        END IF;
    END
    $migration$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ALTER COLUMN "PropertyId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ALTER COLUMN "RoomTypeId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE INDEX "IX_ViewingSchedules_PropertyId_RoomTypeId" ON "ViewingSchedules" ("PropertyId", "RoomTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE INDEX "IX_ViewingSchedules_PropertyId_RoomTypeId_RoomId" ON "ViewingSchedules" ("PropertyId", "RoomTypeId", "RoomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE INDEX "IX_ViewingSchedules_RoomTypeId" ON "ViewingSchedules" ("RoomTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE UNIQUE INDEX "UX_Bookings_ViewingId" ON "Bookings" ("ViewingId") WHERE "ViewingId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Bookings" ADD CONSTRAINT "CK_Bookings_AgreedMonthlyRent_Positive" CHECK ("AgreedMonthlyRent" IS NULL OR "AgreedMonthlyRent" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    CREATE INDEX "IX_PropertyRoomTypes_RoomTypeId_IsActive" ON "PropertyRoomTypes" ("RoomTypeId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Bookings" ADD CONSTRAINT "FK_Bookings_ViewingSchedules_ViewingId" FOREIGN KEY ("ViewingId") REFERENCES "ViewingSchedules" ("ViewingId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "Rooms" ADD CONSTRAINT "FK_Rooms_PropertyRoomTypes_PropertyId_RoomTypeId" FOREIGN KEY ("PropertyId", "RoomTypeId") REFERENCES "PropertyRoomTypes" ("PropertyId", "RoomTypeId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ADD CONSTRAINT "FK_ViewingSchedules_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("PropertyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ADD CONSTRAINT "FK_ViewingSchedules_RoomTypes_RoomTypeId" FOREIGN KEY ("RoomTypeId") REFERENCES "RoomTypes" ("RoomTypeId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    ALTER TABLE "ViewingSchedules" ADD CONSTRAINT "FK_ViewingSchedules_Rooms_PropertyId_RoomTypeId_RoomId" FOREIGN KEY ("PropertyId", "RoomTypeId", "RoomId") REFERENCES "Rooms" ("PropertyId", "RoomTypeId", "RoomId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114085610_PropertyScopedRoomTypesAndViewingFlow') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114085610_PropertyScopedRoomTypesAndViewingFlow', '8.0.10');
    END IF;
END $EF$;
COMMIT;

