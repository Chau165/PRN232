-- PRN232 Phase 3.1D: Property Content & Service Rates seed
-- REVIEW ARTIFACT. Do not execute on Supabase live until the preview is explicitly approved.
-- Preview date: 2026-10-20 (Asia/Ho_Chi_Minh); all rates start 2026-11-01.
-- Writes only Properties.Description, PropertyAmenities, PropertyServiceRates.
-- Does not alter Services.Price or Invoice/Booking/Contract/Maintenance data.

BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

DO $seed_guard$
DECLARE
    expected_count integer;
BEGIN
    IF DATE '2026-11-01' <> (date_trunc('month', timezone('Asia/Ho_Chi_Minh', now()) + interval '1 month'))::date THEN
        RAISE EXCEPTION 'Stale preview: regenerate next-month EffectiveFrom and obtain approval before applying this seed.';
    END IF;

    SELECT count(*) INTO expected_count
    FROM (VALUES
        (1, 'Nhà trọ Bình An'),
        (2, 'Khu trọ Sunrise'),
        (3, 'Nhà trọ Minh Tâm')
    ) AS expected("PropertyId", "PropertyName")
    JOIN "Properties" p USING ("PropertyId", "PropertyName");
    IF expected_count <> 3 THEN
        RAISE EXCEPTION 'Property IDs/names differ from the reviewed preview; no seed applied.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            (1, 'Nhà trọ Bình An', 'Nhà trọ Bình An tại 12 Đường Số 5, Phường Linh Trung, Thủ Đức, TP. Hồ Chí Minh. Trang chi tiết giới thiệu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ.'),
            (2, 'Khu trọ Sunrise', 'Khu trọ Sunrise tọa lạc tại 88 Nguyễn Gia Trí, Phường 25, Bình Thạnh, TP. Hồ Chí Minh. Người xem có thể tra cứu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ.'),
            (3, 'Nhà trọ Minh Tâm', 'Nhà trọ Minh Tâm nằm tại 45 Quang Trung, Phường 10, Gò Vấp, TP. Hồ Chí Minh. Trang chi tiết giới thiệu tiện ích chung, thông tin phòng và các mức phí dịch vụ áp dụng tại khu trọ.')
        ) AS expected("PropertyId", "PropertyName", "Description")
        JOIN "Properties" p USING ("PropertyId", "PropertyName")
        WHERE p."Description" IS NOT NULL AND p."Description" <> expected."Description"
    ) THEN
        RAISE EXCEPTION 'A property description changed since preview; review the current content before applying.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "Amenities" WHERE "AmenityId" = 1 AND "Name" = 'Wi-Fi') THEN
        RAISE EXCEPTION 'AmenityId 1 is no longer Wi-Fi; review the catalog before applying.';
    END IF;

    IF (SELECT count(*) FROM (VALUES
        (1, 'Electricity - per kWh'),
        (2, 'Water - per m3'),
        (3, 'Internet - monthly'),
        (4, 'Trash - monthly')
    ) AS expected("ServiceId", "Name")
    JOIN "Services" s USING ("ServiceId", "Name")) <> 4 THEN
        RAISE EXCEPTION 'Service IDs/names differ from the reviewed preview; no rates applied.';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM (VALUES
            (1, 1, 3500.00::numeric, 'kwh', 'usage'),
            (1, 2, 20000.00::numeric, 'm3', 'usage'),
            (1, 3, 100000.00::numeric, 'room_month', 'fixed_room'),
            (1, 4, 30000.00::numeric, 'room_month', 'fixed_room'),
            (2, 1, 3800.00::numeric, 'kwh', 'usage'),
            (2, 2, 22000.00::numeric, 'm3', 'usage'),
            (2, 3, 100000.00::numeric, 'room_month', 'fixed_room'),
            (2, 4, 30000.00::numeric, 'room_month', 'fixed_room'),
            (3, 1, 3600.00::numeric, 'kwh', 'usage'),
            (3, 2, 20000.00::numeric, 'm3', 'usage'),
            (3, 3, 90000.00::numeric, 'room_month', 'fixed_room'),
            (3, 4, 30000.00::numeric, 'room_month', 'fixed_room')
        ) AS expected("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis")
        JOIN "PropertyServiceRates" r USING ("PropertyId", "ServiceId")
        WHERE daterange(r."EffectiveFrom", r."EffectiveTo", '[)')
              && daterange(DATE '2026-11-01', NULL::date, '[)')
          AND NOT (
              r."EffectiveFrom" = DATE '2026-11-01'
              AND r."EffectiveTo" IS NULL
              AND r."Price" = expected."Price"
              AND r."UnitCode" = expected."UnitCode"
              AND r."BillingBasis" = expected."BillingBasis"
          )
    ) THEN
        RAISE EXCEPTION 'An existing rate overlaps the proposed period; review and reconcile manually. No existing rate was changed.';
    END IF;
END;
$seed_guard$;

UPDATE "Properties" AS p
SET "Description" = expected."Description"
FROM (VALUES
    (1, 'Nhà trọ Bình An', 'Nhà trọ Bình An tại 12 Đường Số 5, Phường Linh Trung, Thủ Đức, TP. Hồ Chí Minh. Trang chi tiết giới thiệu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ.'),
    (2, 'Khu trọ Sunrise', 'Khu trọ Sunrise tọa lạc tại 88 Nguyễn Gia Trí, Phường 25, Bình Thạnh, TP. Hồ Chí Minh. Người xem có thể tra cứu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ.'),
    (3, 'Nhà trọ Minh Tâm', 'Nhà trọ Minh Tâm nằm tại 45 Quang Trung, Phường 10, Gò Vấp, TP. Hồ Chí Minh. Trang chi tiết giới thiệu tiện ích chung, thông tin phòng và các mức phí dịch vụ áp dụng tại khu trọ.')
) AS expected("PropertyId", "PropertyName", "Description")
WHERE p."PropertyId" = expected."PropertyId"
  AND p."PropertyName" = expected."PropertyName"
  AND p."Description" IS DISTINCT FROM expected."Description";

-- Wi-Fi is proposed as a common amenity for the three properties.
-- It already appears as a room amenity on all rentable rooms; the public API returns
-- commonAmenities and roomAmenities separately, so the label will appear in both sections.
INSERT INTO "PropertyAmenities" ("PropertyId", "AmenityId")
SELECT expected."PropertyId", 1
FROM (VALUES (1), (2), (3)) AS expected("PropertyId")
ON CONFLICT ("PropertyId", "AmenityId") DO NOTHING;

WITH expected("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis") AS (
    VALUES
        (1, 1, 3500.00::numeric, 'kwh', 'usage'),
        (1, 2, 20000.00::numeric, 'm3', 'usage'),
        (1, 3, 100000.00::numeric, 'room_month', 'fixed_room'),
        (1, 4, 30000.00::numeric, 'room_month', 'fixed_room'),
        (2, 1, 3800.00::numeric, 'kwh', 'usage'),
        (2, 2, 22000.00::numeric, 'm3', 'usage'),
        (2, 3, 100000.00::numeric, 'room_month', 'fixed_room'),
        (2, 4, 30000.00::numeric, 'room_month', 'fixed_room'),
        (3, 1, 3600.00::numeric, 'kwh', 'usage'),
        (3, 2, 20000.00::numeric, 'm3', 'usage'),
        (3, 3, 90000.00::numeric, 'room_month', 'fixed_room'),
        (3, 4, 30000.00::numeric, 'room_month', 'fixed_room')
)
INSERT INTO "PropertyServiceRates"
    ("PropertyId", "ServiceId", "Price", "UnitCode", "BillingBasis", "EffectiveFrom", "EffectiveTo")
SELECT expected."PropertyId", expected."ServiceId", expected."Price", expected."UnitCode", expected."BillingBasis", DATE '2026-11-01', NULL
FROM expected
WHERE NOT EXISTS (
    SELECT 1
    FROM "PropertyServiceRates" existing
    WHERE existing."PropertyId" = expected."PropertyId"
      AND existing."ServiceId" = expected."ServiceId"
      AND existing."Price" = expected."Price"
      AND existing."UnitCode" = expected."UnitCode"
      AND existing."BillingBasis" = expected."BillingBasis"
      AND existing."EffectiveFrom" = DATE '2026-11-01'
      AND existing."EffectiveTo" IS NULL
);

COMMIT;
