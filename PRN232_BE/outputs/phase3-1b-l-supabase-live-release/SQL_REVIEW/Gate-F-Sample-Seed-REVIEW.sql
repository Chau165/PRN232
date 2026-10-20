-- REVIEW / DISCOVERY ONLY. No live seed payload is approved or mapped.
-- Run only after B/C/D/E postflight, over a verified TLS connection.
-- This file contains no INSERT or UPDATE and is intentionally read-only.
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';

-- Verify requested names and whether a description is eligible for filling.
-- Do not return or overwrite existing non-empty description text.
SELECT "PropertyId",
       "PropertyName",
       ("Description" IS NULL OR BTRIM("Description") = '') AS description_is_empty
FROM public."Properties"
WHERE "PropertyName" IN ('Nhà trọ Bình An', 'Khu trọ Sunrise', 'Nhà trọ Minh Tâm')
ORDER BY "PropertyName", "PropertyId";

-- Catalog only; a room-level assignment does not establish a common amenity.
SELECT a."AmenityId",
       a."Name",
       count(DISTINCT r."PropertyId") AS room_assignment_property_count
FROM public."Amenities" a
LEFT JOIN public."RoomAmenities" ra ON ra."AmenityId" = a."AmenityId"
LEFT JOIN public."Rooms" r ON r."RoomId" = ra."RoomId"
GROUP BY a."AmenityId", a."Name"
ORDER BY a."Name", a."AmenityId";

-- Read service catalog as reference only; this does not approve its price or unit.
SELECT "ServiceId", "Name", "Price"
FROM public."Services"
ORDER BY "Name", "ServiceId";

-- Current assignments and rates for the requested properties, to detect conflicts.
SELECT p."PropertyId",
       p."PropertyName",
       pa."AmenityId",
       a."Name" AS amenity_name
FROM public."Properties" p
JOIN public."PropertyAmenities" pa ON pa."PropertyId" = p."PropertyId"
JOIN public."Amenities" a ON a."AmenityId" = pa."AmenityId"
WHERE p."PropertyName" IN ('Nhà trọ Bình An', 'Khu trọ Sunrise', 'Nhà trọ Minh Tâm')
ORDER BY p."PropertyId", pa."AmenityId";

SELECT p."PropertyId",
       p."PropertyName",
       r."ServiceId",
       s."Name" AS service_name,
       r."Price",
       r."UnitCode",
       r."BillingBasis",
       r."EffectiveFrom",
       r."EffectiveTo"
FROM public."Properties" p
JOIN public."PropertyServiceRates" r ON r."PropertyId" = p."PropertyId"
JOIN public."Services" s ON s."ServiceId" = r."ServiceId"
WHERE p."PropertyName" IN ('Nhà trọ Bình An', 'Khu trọ Sunrise', 'Nhà trọ Minh Tâm')
ORDER BY p."PropertyId", r."ServiceId", r."EffectiveFrom";

-- Proposal values only. IDs intentionally NULL until live catalog is verified.
SELECT *
FROM (VALUES
    ('Nhà trọ Bình An', 'Điện', 3500::numeric, 'kwh', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Bình An', 'Nước', 20000::numeric, 'm3', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Bình An', 'Internet', 100000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Bình An', 'Rác', 30000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Khu trọ Sunrise', 'Điện', 3800::numeric, 'kwh', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Khu trọ Sunrise', 'Nước', 22000::numeric, 'm3', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Khu trọ Sunrise', 'Internet', 100000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Khu trọ Sunrise', 'Rác', 30000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Minh Tâm', 'Điện', 3600::numeric, 'kwh', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Minh Tâm', 'Nước', 20000::numeric, 'm3', 'usage', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Minh Tâm', 'Internet', 90000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL'),
    ('Nhà trọ Minh Tâm', 'Rác', 30000::numeric, 'room_month', 'fixed_room', DATE '2026-11-01', NULL::date, NULL::integer, 'DEMO / NOT CONTRACTUAL')
) AS proposal(property_name, service_label, proposed_price, unit_code, billing_basis,
              effective_from, effective_to, service_id, approval_label);

DO $seed_guard$
BEGIN
    IF current_setting('app.phase31b_l_seed_approved', true) IS DISTINCT FROM 'true' THEN
        RAISE NOTICE 'BLOCKED: owner has not approved the exact preview; no writes are present in this file.';
        RETURN;
    END IF;
    RAISE EXCEPTION 'BLOCKED: this review file has no verified IDs or write payload; replace it only after approval and isolated rehearsal.';
END
$seed_guard$;

ROLLBACK;