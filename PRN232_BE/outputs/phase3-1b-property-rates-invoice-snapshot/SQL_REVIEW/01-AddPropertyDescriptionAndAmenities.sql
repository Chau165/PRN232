START TRANSACTION;

ALTER TABLE "Properties" ADD "Description" text;

CREATE TABLE "PropertyAmenities" (
    "PropertyId" integer NOT NULL,
    "AmenityId" integer NOT NULL,
    CONSTRAINT "PK_PropertyAmenities" PRIMARY KEY ("PropertyId", "AmenityId"),
    CONSTRAINT "FK_PropertyAmenities_Amenities_AmenityId" FOREIGN KEY ("AmenityId") REFERENCES "Amenities" ("AmenityId") ON DELETE CASCADE,
    CONSTRAINT "FK_PropertyAmenities_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("PropertyId") ON DELETE CASCADE
);

CREATE INDEX "IX_PropertyAmenities_AmenityId" ON "PropertyAmenities" ("AmenityId");

ALTER TABLE "PropertyAmenities" ENABLE ROW LEVEL SECURITY;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261019143543_AddPropertyDescriptionAndAmenities', '8.0.10');

COMMIT;

