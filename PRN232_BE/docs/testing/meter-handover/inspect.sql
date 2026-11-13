-- Read-only evidence; no customer names, credentials or business row contents are returned.
-- Normalize timestamptz JSON serialization between the Supabase session and the local restore.
SET TIME ZONE 'UTC';
SELECT jsonb_pretty(jsonb_build_object(
    'counts', jsonb_build_object(
        'Contracts', (SELECT count(*) FROM public."Contracts"),
        'UtilityReadings', (SELECT count(*) FROM public."UtilityReadings"),
        'Invoices', (SELECT count(*) FROM public."Invoices"),
        'InvoiceDetails', (SELECT count(*) FROM public."InvoiceDetails"),
        'Payments', (SELECT count(*) FROM public."Payments"),
        'Bookings', (SELECT count(*) FROM public."Bookings"),
        'PropertyManagerAssignments', (SELECT count(*) FROM public."PropertyManagerAssignments")),
    'businessHashes', jsonb_build_object(
        'Contracts', (SELECT md5(jsonb_agg(to_jsonb(record) - 'InitialElectricReading' - 'InitialWaterReading' - 'MeterHandoverDate' ORDER BY "ContractId")::text) FROM public."Contracts" record),
        'UtilityReadings', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "ReadingId")::text) FROM public."UtilityReadings" record),
        'Invoices', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "InvoiceId")::text) FROM public."Invoices" record),
        'InvoiceDetails', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "InvoiceDetailId")::text) FROM public."InvoiceDetails" record),
        'Payments', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "PaymentId")::text) FROM public."Payments" record),
        'Bookings', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "BookingId")::text) FROM public."Bookings" record),
        'PropertyManagerAssignments', (SELECT md5(jsonb_agg(to_jsonb(record) ORDER BY "PropertyId", "ManagerId")::text) FROM public."PropertyManagerAssignments" record)),
    'migrations', (SELECT jsonb_agg(record ORDER BY "MigrationId") FROM public."__EFMigrationsHistory" record),
    'baselineColumns', (SELECT jsonb_agg(record ORDER BY column_name) FROM (
        SELECT column_name, data_type, is_nullable, column_default FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'Contracts'
          AND column_name IN ('InitialElectricReading', 'InitialWaterReading', 'MeterHandoverDate')) record),
    'unchangedSchemaHash', (SELECT md5(jsonb_agg(record ORDER BY table_name, ordinal_position)::text) FROM (
        SELECT table_name, ordinal_position, column_name, data_type, is_nullable, column_default
        FROM information_schema.columns WHERE table_schema = 'public'
          AND NOT (table_name = 'Contracts' AND column_name IN ('InitialElectricReading', 'InitialWaterReading', 'MeterHandoverDate'))) record)
));
