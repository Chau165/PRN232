-- Review-only SQL matching migration 20261104032932_AddPropertyManagerAssignments.
-- Do not run until the migration and the target database have been explicitly approved.
BEGIN;

CREATE TABLE "PropertyManagerAssignments" (
    "PropertyId" integer NOT NULL,
    "ManagerId" integer NOT NULL,
    CONSTRAINT "PK_PropertyManagerAssignments" PRIMARY KEY ("PropertyId", "ManagerId"),
    CONSTRAINT "FK_PropertyManagerAssignments_Properties_PropertyId"
        FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("PropertyId") ON DELETE RESTRICT,
    CONSTRAINT "FK_PropertyManagerAssignments_Users_ManagerId"
        FOREIGN KEY ("ManagerId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT
);

CREATE INDEX "IX_PropertyManagerAssignments_ManagerId"
    ON "PropertyManagerAssignments" ("ManagerId");

ALTER TABLE "PropertyManagerAssignments" ENABLE ROW LEVEL SECURITY;
REVOKE ALL PRIVILEGES ON TABLE "PropertyManagerAssignments" FROM PUBLIC, anon, authenticated, service_role;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261104032932_AddPropertyManagerAssignments', '8.0.10');

COMMIT;
