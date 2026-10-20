# Phase 3.1D — Supabase live write report

## Execution

The owner approved the seed SQL with SHA-256:

B6575D2F331281F650C1E70AED22B489A651F3AC7DF0C9F10E19B512AB5E0B44

Immediately before execution, a fresh read-only preflight connected through the existing Windows User Environment configuration. The connection was verified as aws-0-ap-southeast-1.pooler.supabase.com:5432/postgres using TLS VerifyFull. PostgreSQL reported version 17.11 and Vietnam date 2026-10-20. The three reviewed property IDs/names still matched, descriptions were NULL, AmenityId 1 was Wi-Fi, ServiceIds 1–4 and their legacy prices matched the preview, and both PropertyAmenities and PropertyServiceRates were empty.

The approved SQL file hash matched exactly. The transaction completed and committed.

## Rows now present on Supabase live

| PropertyId | Property | Description |
|---:|---|---|
| 1 | Nhà trọ Bình An | Nhà trọ Bình An tại 12 Đường Số 5, Phường Linh Trung, Thủ Đức, TP. Hồ Chí Minh. Trang chi tiết giới thiệu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ. |
| 2 | Khu trọ Sunrise | Khu trọ Sunrise tọa lạc tại 88 Nguyễn Gia Trí, Phường 25, Bình Thạnh, TP. Hồ Chí Minh. Người xem có thể tra cứu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ. |
| 3 | Nhà trọ Minh Tâm | Nhà trọ Minh Tâm nằm tại 45 Quang Trung, Phường 10, Gò Vấp, TP. Hồ Chí Minh. Trang chi tiết giới thiệu tiện ích chung, thông tin phòng và các mức phí dịch vụ áp dụng tại khu trọ. |

PropertyAmenities contains (PropertyId, AmenityId): (1, 1), (2, 1), (3, 1); AmenityId 1 is Wi-Fi.

| RateId | PropertyId | ServiceId | Service | Price | UnitCode | BillingBasis | EffectiveFrom |
|---:|---:|---:|---|---:|---|---|---|
| 1 | 1 | 1 | Electricity - per kWh | 3,500.00 | kwh | usage | 2026-11-01 |
| 2 | 1 | 2 | Water - per m3 | 20,000.00 | m3 | usage | 2026-11-01 |
| 3 | 1 | 3 | Internet - monthly | 100,000.00 | room_month | fixed_room | 2026-11-01 |
| 4 | 1 | 4 | Trash - monthly | 30,000.00 | room_month | fixed_room | 2026-11-01 |
| 5 | 2 | 1 | Electricity - per kWh | 3,800.00 | kwh | usage | 2026-11-01 |
| 6 | 2 | 2 | Water - per m3 | 22,000.00 | m3 | usage | 2026-11-01 |
| 7 | 2 | 3 | Internet - monthly | 100,000.00 | room_month | fixed_room | 2026-11-01 |
| 8 | 2 | 4 | Trash - monthly | 30,000.00 | room_month | fixed_room | 2026-11-01 |
| 9 | 3 | 1 | Electricity - per kWh | 3,600.00 | kwh | usage | 2026-11-01 |
| 10 | 3 | 2 | Water - per m3 | 20,000.00 | m3 | usage | 2026-11-01 |
| 11 | 3 | 3 | Internet - monthly | 90,000.00 | room_month | fixed_room | 2026-11-01 |
| 12 | 3 | 4 | Trash - monthly | 30,000.00 | room_month | fixed_room | 2026-11-01 |

## Read-only postflight

Postflight passed on Supabase live:

- 3/3 expected property descriptions match.
- 3/3 expected common Wi-Fi links exist.
- 12/12 exact rate rows exist; PropertyServiceRates total is 12.
- PropertyAmenities total is 3.
- All 4 legacy Services.Price values remain unchanged.
- No seed statement wrote to Invoice, Booking, Contract, or Maintenance tables.

## Property Detail API smoke test

The existing local API port 5102 was not listening. A temporary local BE process was started on loopback port 55102 using the same User Environment connection with TLS VerifyFull and a process-only PropertyDetailEnrichmentEnabled=true flag. The GET /api/public/properties/{id} endpoint returned HTTP 200 for IDs 1, 2, and 3. Each response contained the exact approved description and common Wi-Fi amenity.

The API returned zero livingCosts for all three properties because the rates begin on 2026-11-01 and the verified Vietnam date was 2026-10-20. This is the expected effective-date behavior. The temporary API process was stopped after the smoke test.

The API flag was set only for this local process. The live/deployed API runtime configuration was not modified or verified; it must enable PublicBrowsing:PropertyDetailEnrichmentEnabled for the enriched fields to be returned there.

## Scope

No source code was edited. No migration was run on Supabase. No Invoice, Booking, Contract, or Maintenance data was changed. No stage, commit, push, or deployment was performed.
