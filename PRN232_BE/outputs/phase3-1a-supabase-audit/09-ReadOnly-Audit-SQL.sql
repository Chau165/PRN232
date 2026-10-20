-- Phase 3.1A live catalog and aggregate audit.
-- Run only in the configured PRN232 Supabase connection.
-- Every data/catalog statement below is SELECT. No PII columns are selected.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10000ms';

-- Q: CURRENT_CONTEXT
SELECT to_char(transaction_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"') AS snapshot_utc,
       current_database() AS database_name,
       current_schema() AS schema_name,
       current_setting('transaction_read_only') AS transaction_read_only,
       current_setting('server_version') AS server_version;

-- Q: PUBLIC_TABLES
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
ORDER BY table_name;

-- Q: RELEVANT_COLUMNS
SELECT table_name, column_name, data_type, is_nullable, column_default,
       numeric_precision, numeric_scale, is_identity, identity_generation
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = ANY (ARRAY[
    'Properties','Rooms','RoomTypes','Amenities','RoomAmenities',
    'Services','RoomTypeServices','Invoices','InvoiceDetails','Payments',
    'UtilityReadings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages','Contracts','Bookings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages','__EFMigrationsHistory'
  ])
ORDER BY table_name, ordinal_position;

-- Q: RELEVANT_CONSTRAINTS
SELECT r.relname AS table_name, con.conname AS constraint_name,
       CASE con.contype WHEN 'p' THEN 'PRIMARY KEY' WHEN 'f' THEN 'FOREIGN KEY'
         WHEN 'u' THEN 'UNIQUE' WHEN 'c' THEN 'CHECK' WHEN 'x' THEN 'EXCLUDE'
         ELSE con.contype::text END AS kind,
       pg_get_constraintdef(con.oid) AS definition
FROM pg_constraint con
JOIN pg_class r ON r.oid = con.conrelid
JOIN pg_namespace n ON n.oid = r.relnamespace
WHERE n.nspname = 'public'
  AND r.relname = ANY (ARRAY[
    'Properties','Rooms','RoomTypes','Amenities','RoomAmenities',
    'Services','RoomTypeServices','Invoices','InvoiceDetails','Payments',
    'UtilityReadings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages','Contracts','Bookings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages'
  ])
ORDER BY r.relname, con.conname;

-- Q: RELEVANT_INDEXES
SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = ANY (ARRAY[
    'Properties','Rooms','RoomTypes','Amenities','RoomAmenities',
    'Services','RoomTypeServices','Invoices','InvoiceDetails','Payments',
    'UtilityReadings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages','Contracts','Bookings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages'
  ])
ORDER BY tablename, indexname;

-- Q: RLS_AND_POLICY_COUNTS
SELECT c.relname AS table_name, c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS rls_forced, count(p.policyname) AS policy_coun
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_policies p ON p.schemaname = n.nspname AND p.tablename = c.relname
WHERE n.nspname = 'public' AND c.relkind = 'r'
  AND c.relname = ANY (ARRAY[
    'Properties','Rooms','RoomTypes','Amenities','RoomAmenities',
    'Services','RoomTypeServices','Invoices','InvoiceDetails','Payments',
    'UtilityReadings','MaintenanceLogs','MaintenanceReports','MaintenanceReportImages'
  ])
GROUP BY c.relname, c.relrowsecurity, c.relforcerowsecurity
ORDER BY c.relname;

-- Q: CURRENT_ROLE_RLS_FLAGS
SELECT coalesce(bool_or(rolsuper), false) AS current_role_is_superuser, coalesce(bool_or(rolbypassrls), false) AS current_role_bypasses_rls FROM pg_roles WHERE rolname = current_user;

-- Q: APPLIED_EF_MIGRATIONS
SELECT "MigrationId", "ProductVersion"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

-- Q: PROPOSED_DETAIL_TABLES_EXIST
SELECT to_regclass('public."PropertyAmenities"') IS NOT NULL AS property_amenities_exists,
       to_regclass('public."PropertyServices"') IS NOT NULL AS property_services_exists,
       to_regclass('public."PropertyServiceRates"') IS NOT NULL AS property_service_rates_exists,
       to_regclass('public."ServiceRates"') IS NOT NULL AS service_rates_exists,
       to_regclass('public."UtilityRates"') IS NOT NULL AS utility_rates_exists;

-- Q: TARGET_PROPERTY_SUMMARY
WITH as_of AS (
  SELECT transaction_timestamp() AS instant_utc
),
rentable_rooms AS (
  SELECT r."RoomId", r."PropertyId", r."RoomTypeId", rt."Price"
  FROM public."Rooms" r
  JOIN public."RoomTypes" rt ON rt."RoomTypeId" = r."RoomTypeId"
  CROSS JOIN as_of a
  WHERE r."Status" = 'Available'
    AND NOT EXISTS (
      SELECT 1 FROM public."Bookings" b
      WHERE b."RoomId" = r."RoomId"
        AND (b."Status" = 'Deposited'
          OR (b."Status" = 'Pending' AND b."ExpiryDate" > a.instant_utc)
          OR b."Status" NOT IN ('Pending','Deposited','Cancelled'))
    )
    AND NOT EXISTS (
      SELECT 1 FROM public."Contracts" c
      WHERE c."RoomId" = r."RoomId"
        AND (c."Status" = 'Active' OR c."Status" NOT IN ('Active'))
    )
)
SELECT p."PropertyId", p."PropertyName",
       count(DISTINCT r."RoomId") AS total_room_count,
       count(DISTINCT rr."RoomId") AS rentable_room_count,
       min(rr."Price") AS starting_rent,
       coalesce(nullif(btrim(p."PrimaryImageUrl"), ''), '') <> '' AS has_primary_image,
       count(DISTINCT (ra."RoomId", ra."AmenityId"))
         FILTER (WHERE ra."RoomId" IS NOT NULL) AS amenity_links_all_rooms,
       count(DISTINCT (ra."RoomId", ra."AmenityId"))
         FILTER (WHERE rr."RoomId" IS NOT NULL) AS amenity_links_rentable_rooms,
       count(DISTINCT rts."ServiceId") AS services_across_all_room_types,
       count(DISTINCT rts."ServiceId")
         FILTER (WHERE rr."RoomId" IS NOT NULL) AS services_across_rentable_room_types
FROM public."Properties" p
LEFT JOIN public."Rooms" r ON r."PropertyId" = p."PropertyId"
LEFT JOIN public."RoomAmenities" ra ON ra."RoomId" = r."RoomId"
LEFT JOIN public."RoomTypeServices" rts ON rts."RoomTypeId" = r."RoomTypeId"
LEFT JOIN rentable_rooms rr ON rr."RoomId" = r."RoomId"
WHERE lower(p."PropertyName") IN (
  lower('Nhà trọ Bình An'), lower('Khu trọ Sunrise'), lower('Nhà trọ Minh Tâm')
)
GROUP BY p."PropertyId", p."PropertyName", p."PrimaryImageUrl"
ORDER BY p."PropertyId";

-- Q: TARGET_ROOM_AMENITY_COVERAGE
WITH as_of AS (
  SELECT transaction_timestamp() AS instant_utc
),
rentable_rooms AS (
  SELECT r."RoomId", r."PropertyId"
  FROM public."Rooms" r
  CROSS JOIN as_of a
  WHERE r."Status" = 'Available'
    AND NOT EXISTS (
      SELECT 1 FROM public."Bookings" b
      WHERE b."RoomId" = r."RoomId"
        AND (b."Status" = 'Deposited'
          OR (b."Status" = 'Pending' AND b."ExpiryDate" > a.instant_utc)
          OR b."Status" NOT IN ('Pending','Deposited','Cancelled'))
    )
    AND NOT EXISTS (
      SELECT 1 FROM public."Contracts" c
      WHERE c."RoomId" = r."RoomId"
        AND (c."Status" = 'Active' OR c."Status" NOT IN ('Active'))
    )
)
SELECT p."PropertyId", p."PropertyName", a."AmenityId", a."Name",
       count(DISTINCT r."RoomId") FILTER (WHERE ra."RoomId" IS NOT NULL) AS rooms_with_amenity,
       count(DISTINCT rr."RoomId") AS rentable_rooms_with_amenity
FROM public."Properties" p
JOIN public."Rooms" r ON r."PropertyId" = p."PropertyId"
JOIN public."RoomAmenities" ra ON ra."RoomId" = r."RoomId"
JOIN public."Amenities" a ON a."AmenityId" = ra."AmenityId"
LEFT JOIN rentable_rooms rr ON rr."RoomId" = r."RoomId"
WHERE lower(p."PropertyName") IN (
  lower('Nhà trọ Bình An'), lower('Khu trọ Sunrise'), lower('Nhà trọ Minh Tâm')
)
GROUP BY p."PropertyId", p."PropertyName", a."AmenityId", a."Name"
ORDER BY p."PropertyId", a."AmenityId";

-- Q: ROOM_AMENITY_LINK_INTEGRITY
SELECT count(*) AS link_count,
       count(*) FILTER (WHERE r."RoomId" IS NULL) AS orphan_room_links,
       count(*) FILTER (WHERE a."AmenityId" IS NULL) AS orphan_amenity_links,
       count(*) - count(DISTINCT (ra."RoomId", ra."AmenityId")) AS duplicate_links
FROM public."RoomAmenities" ra
LEFT JOIN public."Rooms" r ON r."RoomId" = ra."RoomId"
LEFT JOIN public."Amenities" a ON a."AmenityId" = ra."AmenityId";

-- Q: PROPERTY_SERVICE_COUNTS
WITH as_of AS (
  SELECT transaction_timestamp() AS instant_utc
),
rentable_rooms AS (
  SELECT r."RoomId", r."PropertyId", r."RoomTypeId"
  FROM public."Rooms" r
  CROSS JOIN as_of a
  WHERE r."Status" = 'Available'
    AND NOT EXISTS (
      SELECT 1 FROM public."Bookings" b
      WHERE b."RoomId" = r."RoomId"
        AND (b."Status" = 'Deposited'
          OR (b."Status" = 'Pending' AND b."ExpiryDate" > a.instant_utc)
          OR b."Status" NOT IN ('Pending','Deposited','Cancelled'))
    )
    AND NOT EXISTS (
      SELECT 1 FROM public."Contracts" c
      WHERE c."RoomId" = r."RoomId"
        AND (c."Status" = 'Active' OR c."Status" NOT IN ('Active'))
    )
)
SELECT p."PropertyId", p."PropertyName",
       count(DISTINCT rts."ServiceId") FILTER (WHERE r."RoomId" IS NOT NULL) AS services_through_any_room_type,
       count(DISTINCT rts."ServiceId") FILTER (WHERE rr."RoomId" IS NOT NULL) AS services_through_rentable_room_types
FROM public."Properties" p
LEFT JOIN public."Rooms" r ON r."PropertyId" = p."PropertyId"
LEFT JOIN public."RoomTypeServices" rts ON rts."RoomTypeId" = r."RoomTypeId"
LEFT JOIN rentable_rooms rr ON rr."RoomId" = r."RoomId"
WHERE lower(p."PropertyName") IN (
  lower('Nhà trọ Bình An'), lower('Khu trọ Sunrise'), lower('Nhà trọ Minh Tâm')
)
GROUP BY p."PropertyId", p."PropertyName"
ORDER BY p."PropertyId";

-- Q: SERVICE_CATALOG_AND_SCOPE
SELECT s."ServiceId", s."Name", s."Price",
       count(DISTINCT rts."RoomTypeId") AS linked_room_type_count,
       count(DISTINCT r."PropertyId") AS linked_property_count,
       count(DISTINCT r."RoomId") AS linked_room_coun
FROM public."Services" s
LEFT JOIN public."RoomTypeServices" rts ON rts."ServiceId" = s."ServiceId"
LEFT JOIN public."Rooms" r ON r."RoomTypeId" = rts."RoomTypeId"
GROUP BY s."ServiceId", s."Name", s."Price"
ORDER BY s."ServiceId";

-- Q: SERVICE_LINK_INTEGRITY
SELECT count(*) AS link_count,
       count(*) FILTER (WHERE rt."RoomTypeId" IS NULL) AS orphan_room_type_links,
       count(*) FILTER (WHERE s."ServiceId" IS NULL) AS orphan_service_links,
       count(*) - count(DISTINCT (rts."RoomTypeId", rts."ServiceId")) AS duplicate_links
FROM public."RoomTypeServices" rts
LEFT JOIN public."RoomTypes" rt ON rt."RoomTypeId" = rts."RoomTypeId"
LEFT JOIN public."Services" s ON s."ServiceId" = rts."ServiceId";

-- Q: SERVICE_PRICE_SUMMARY
SELECT count(*) AS service_count,
       count(*) FILTER (WHERE "Price" IS NULL) AS null_price_count,
       count(*) FILTER (WHERE "Price" = 0) AS zero_price_count,
       count(*) FILTER (WHERE "Price" < 0) AS negative_price_count,
       min("Price") AS min_price, max("Price") AS max_price,
       count(DISTINCT "Price") AS distinct_price_coun
FROM public."Services";

-- Q: ROOMTYPE_MULTI_PROPERTY_SHARING
SELECT count(*) AS room_type_ids_used_by_multiple_properties
FROM (
  SELECT rt."RoomTypeId"
  FROM public."RoomTypes" r
  JOIN public."Rooms" r ON r."RoomTypeId" = rt."RoomTypeId"
  GROUP BY rt."RoomTypeId"
  HAVING count(DISTINCT r."PropertyId") > 1
) shared;

-- Q: DUPLICATE_ROOMTYPE_NAMES
SELECT count(*) AS duplicate_type_name_groups,
       coalesce(sum(type_count), 0) AS room_type_ids_in_duplicate_name_groups
FROM (
  SELECT "TypeName", count(*) AS type_coun
  FROM public."RoomTypes"
  GROUP BY "TypeName"
  HAVING count(*) > 1
) repeated;

-- Q: CORE_TABLE_ROW_COUNTS
SELECT 'Properties' AS table_name, count(*)::bigint AS row_count FROM public."Properties"
UNION ALL SELECT 'Rooms', count(*)::bigint FROM public."Rooms"
UNION ALL SELECT 'RoomTypes', count(*)::bigint FROM public."RoomTypes"
UNION ALL SELECT 'Amenities', count(*)::bigint FROM public."Amenities"
UNION ALL SELECT 'RoomAmenities', count(*)::bigint FROM public."RoomAmenities"
UNION ALL SELECT 'Services', count(*)::bigint FROM public."Services"
UNION ALL SELECT 'RoomTypeServices', count(*)::bigint FROM public."RoomTypeServices"
UNION ALL SELECT 'Invoices', count(*)::bigint FROM public."Invoices"
UNION ALL SELECT 'InvoiceDetails', count(*)::bigint FROM public."InvoiceDetails"
UNION ALL SELECT 'UtilityReadings', count(*)::bigint FROM public."UtilityReadings"
UNION ALL SELECT 'Payments', count(*)::bigint FROM public."Payments"
UNION ALL SELECT 'MaintenanceLogs', count(*)::bigint FROM public."MaintenanceLogs"
UNION ALL SELECT 'MaintenanceReports', count(*)::bigint FROM public."MaintenanceReports"
UNION ALL SELECT 'MaintenanceReportImages', count(*)::bigint FROM public."MaintenanceReportImages"
ORDER BY table_name;

-- Q: INVOICE_STATUS_COUNTS
SELECT "Status", count(*) AS invoice_coun
FROM public."Invoices"
GROUP BY "Status"
ORDER BY "Status";

-- Q: INVOICE_DETAIL_ITEM_TYPE_COUNTS
SELECT "ItemType", count(*) AS detail_coun
FROM public."InvoiceDetails"
GROUP BY "ItemType"
ORDER BY "ItemType";

-- Q: UTILITY_READING_SUMMARY
SELECT count(*) AS reading_count,
       count(DISTINCT u."ContractId") AS contracts_with_readings,
       count(DISTINCT u."ReadingMonth") AS months_with_readings,
       count(*) - count(DISTINCT (u."ContractId", u."ReadingMonth")) AS duplicate_contract_month_rows,
       count(*) FILTER (WHERE c."ContractId" IS NULL) AS orphan_contract_rows
FROM public."UtilityReadings" u
LEFT JOIN public."Contracts" c ON c."ContractId" = u."ContractId";

-- Q: PAYMENT_REFERENCE_COUNT_DISTRIBUTION
SELECT num_nonnulls("BookingId","ContractId","InvoiceId","ReadingId") AS referenced_target_count, count(*) AS payment_count FROM public."Payments" GROUP BY num_nonnulls("BookingId","ContractId","InvoiceId","ReadingId") ORDER BY referenced_target_count;

-- Q: PAYMENT_ASSOCIATION_SHAPE
SELECT count(*) AS payment_count,
       count(*) FILTER (WHERE "BookingId" IS NOT NULL) AS with_booking,
       count(*) FILTER (WHERE "ContractId" IS NOT NULL) AS with_contract,
       count(*) FILTER (WHERE "InvoiceId" IS NOT NULL) AS with_invoice,
       count(*) FILTER (WHERE "ReadingId" IS NOT NULL) AS with_reading,
       count(*) FILTER (
         WHERE num_nonnulls("BookingId","ContractId","InvoiceId","ReadingId") <> 1
       ) AS rows_not_linked_to_exactly_one_targe
FROM public."Payments";

ROLLBACK;
