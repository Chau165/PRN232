# Phase 3.1D preview — Property Content & Service Rates

**Status:** Review only. No seed writes have been made to Supabase live.  
**Live read:** completed using the existing Windows User Environment connection with TLS VerifyFull; catalog reads ran in read-only transactions.  
**Preview date:** 2026-10-20 (Asia/Ho_Chi_Minh).  
**Proposed rate start:** 2026-11-01, the first day of next month.

## Property content

| PropertyId | Property | Address verified from live DB | Proposed Vietnamese description |
|---:|---|---|---|
| 1 | Nhà trọ Bình An | 12 Đường Số 5, Phường Linh Trung, Thủ Đức, TP. Hồ Chí Minh | Nhà trọ Bình An tại 12 Đường Số 5, Phường Linh Trung, Thủ Đức, TP. Hồ Chí Minh. Trang chi tiết giới thiệu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ. |
| 2 | Khu trọ Sunrise | 88 Nguyễn Gia Trí, Phường 25, Bình Thạnh, TP. Hồ Chí Minh | Khu trọ Sunrise tọa lạc tại 88 Nguyễn Gia Trí, Phường 25, Bình Thạnh, TP. Hồ Chí Minh. Người xem có thể tra cứu thông tin phòng, tiện ích chung và các mức phí dịch vụ của khu trọ. |
| 3 | Nhà trọ Minh Tâm | 45 Quang Trung, Phường 10, Gò Vấp, TP. Hồ Chí Minh | Nhà trọ Minh Tâm nằm tại 45 Quang Trung, Phường 10, Gò Vấp, TP. Hồ Chí Minh. Trang chi tiết giới thiệu tiện ích chung, thông tin phòng và các mức phí dịch vụ áp dụng tại khu trọ. |

All three live Description values are currently NULL.

## Common amenity proposal

| PropertyId | AmenityId | Amenity | Evidence / review note |
|---:|---:|---|---|
| 1 | 1 | Wi-Fi | Present in 1/1 currently rentable rooms |
| 2 | 1 | Wi-Fi | Present in 2/2 currently rentable rooms |
| 3 | 1 | Wi-Fi | Present in 1/1 currently rentable rooms |

The live Amenities catalog contains IDs 1–6: Wi-Fi, Air conditioner, Balcony, Private kitchen, Mezzanine, Shared washing machine. PropertyAmenities is empty. Wi-Fi is the only listed amenity present in every currently rentable room across all three properties, so it is the only catalog item proposed as common. This is evidence of room coverage, not proof of a separate shared-area Wi-Fi service. The API shows commonAmenities and roomAmenities in separate sections; if approved, Wi-Fi will appear in both. Shared washing machine is not proposed for Sunrise because it appears in only 1 of its 2 currently rentable rooms.

## Proposed service rates

Service names and IDs were read from live Services. The Services.Price values below are legacy reference values and will not be changed. PropertyServiceRates is currently empty.

| PropertyId | Property | ServiceId | Service | Proposed price | UnitCode | BillingBasis | EffectiveFrom | Current legacy Services.Price |
|---:|---|---:|---|---:|---|---|---|---:|
| 1 | Bình An | 1 | Electricity - per kWh | 3,500 đ/kWh | kwh | usage | 2026-11-01 | 3,500.00 |
| 1 | Bình An | 2 | Water - per m3 | 20,000 đ/m³ | m3 | usage | 2026-11-01 | 20,000.00 |
| 1 | Bình An | 3 | Internet - monthly | 100,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 100,000.00 |
| 1 | Bình An | 4 | Trash - monthly | 30,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 30,000.00 |
| 2 | Sunrise | 1 | Electricity - per kWh | 3,800 đ/kWh | kwh | usage | 2026-11-01 | 3,500.00 |
| 2 | Sunrise | 2 | Water - per m3 | 22,000 đ/m³ | m3 | usage | 2026-11-01 | 20,000.00 |
| 2 | Sunrise | 3 | Internet - monthly | 100,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 100,000.00 |
| 2 | Sunrise | 4 | Trash - monthly | 30,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 30,000.00 |
| 3 | Minh Tâm | 1 | Electricity - per kWh | 3,600 đ/kWh | kwh | usage | 2026-11-01 | 3,500.00 |
| 3 | Minh Tâm | 2 | Water - per m3 | 20,000 đ/m³ | m3 | usage | 2026-11-01 | 20,000.00 |
| 3 | Minh Tâm | 3 | Internet - monthly | 90,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 100,000.00 |
| 3 | Minh Tâm | 4 | Trash - monthly | 30,000 đ/phòng/tháng | room_month | fixed_room | 2026-11-01 | 30,000.00 |

The chosen unit/billing pairs and month-start date satisfy the current migration constraints. Rate rows are per property and service. No legacy price, invoice line, or other module data will be updated.

## Idempotency and safeguards

The accompanying SQL checks the reviewed property, amenity, and service IDs/names; verifies the proposed start date is still next month; and stops if any different rate overlaps the proposed period or if a description changed after preview. It updates only the three descriptions, inserts the three property/amenity links with conflict-safe behavior, and inserts the twelve rates only when the exact row is absent. It does not close or rewrite existing rate history.

## API timing

The public Property Detail endpoint evaluates rates using the current date in Asia/Ho_Chi_Minh and has no effective-date query parameter. If this preview is approved and seeded during October 2026, descriptions and common amenities can appear immediately, but the November rates will not appear in livingCosts until 2026-11-01. The endpoint can be smoke-tested after seeding, but its October response should correctly omit those future rates.

## Approval requested

Review the three descriptions, the Wi-Fi common-amenity proposal (including its duplicate appearance across the API's common and room sections), and all twelve prices. No live write will happen until you explicitly approve this preview. If approval or execution occurs after 2026-10-31, regenerate the effective date and preview first.
