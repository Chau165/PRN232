START TRANSACTION;

ALTER TABLE "InvoiceDetails" ADD "BillingBasis" text;

ALTER TABLE "InvoiceDetails" ADD "Quantity" numeric(18,4);

ALTER TABLE "InvoiceDetails" ADD "RateId" integer;

ALTER TABLE "InvoiceDetails" ADD "ServiceId" integer;

ALTER TABLE "InvoiceDetails" ADD "UnitCode" text;

ALTER TABLE "InvoiceDetails" ADD "UnitPrice" numeric(18,2);

CREATE INDEX "IX_InvoiceDetails_RateId_ServiceId" ON "InvoiceDetails" ("RateId", "ServiceId");

CREATE INDEX "IX_InvoiceDetails_ServiceId" ON "InvoiceDetails" ("ServiceId");

ALTER TABLE "InvoiceDetails" ADD CONSTRAINT "CK_InvoiceDetails_RateRequiresService" CHECK ("RateId" IS NULL OR "ServiceId" IS NOT NULL);

ALTER TABLE "InvoiceDetails" ADD CONSTRAINT "FK_InvoiceDetails_PropertyServiceRates_RateId_ServiceId" FOREIGN KEY ("RateId", "ServiceId") REFERENCES "PropertyServiceRates" ("RateId", "ServiceId") ON DELETE RESTRICT;

ALTER TABLE "InvoiceDetails" ADD CONSTRAINT "FK_InvoiceDetails_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("ServiceId") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261019143633_AddInvoiceDetailBillingSnapshot', '8.0.10');

COMMIT;

