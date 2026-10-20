# 07 — Seed preview for the three properties

**Status: PREVIEW ONLY — owner approval and live catalog resolution are pending. No IDs were guessed.**

The descriptions and prices below are proposals copied from the attached prompt, not verified business facts. Since the TLS gate blocked live reads, PropertyId, AmenityId, ServiceId, existing descriptions, current rates, and name-to-service mappings are all NOT VERIFIED.

## Description proposals

| Property name from request | PropertyId | Proposed description | Approval |
|---|---:|---|---|
| Nhà trọ Bình An | NOT VERIFIED | Nhà trọ Bình An tại khu vực Linh Trung, Thủ Đức. Thông tin chi tiết về tiện ích và giá dịch vụ được cập nhật riêng theo từng phòng và khu trọ. | PENDING |
| Khu trọ Sunrise | NOT VERIFIED | Khu trọ Sunrise cung cấp các lựa chọn phòng cho thuê tại TP. Hồ Chí Minh. Xem danh sách phòng và tiện ích theo thông tin đang có hiệu lực trên hệ thống. | PENDING |
| Nhà trọ Minh Tâm | NOT VERIFIED | Nhà trọ Minh Tâm tại khu vực Gò Vấp. Các tiện ích chung và chi phí sinh hoạt được công bố theo cấu hình của khu trọ. | PENDING |

These statements are not approved or independently verified. Preserve any existing non-empty description.

## Amenity preview

No amenity assignment is proposed. The live Amenities catalogue and actual AmenityId values were not read, and a room amenity does not prove a property-wide amenity. The owner must select catalogue entries and confirm they are common to the property before any insert. No camera, lift, parking, Wi-Fi, laundry, or other amenity is asserted.

## Proposed rates (DEMO / NOT CONTRACTUAL)

Proposed effective date is 2026-11-01 (first day of next month in Asia/Ho_Chi_Minh). EffectiveTo=NULL is only a proposal for an open-ended range; do not use it unless live overlap checks pass. Do not close or replace any existing rate.

| Property | Service label (not verified DB name) | ServiceId | Proposed price | UnitCode / BillingBasis | EffectiveFrom / To | Approval |
|---|---|---:|---:|---|---|---|
| Nhà trọ Bình An | Điện | NOT VERIFIED | 3,500 | kwh / usage | 2026-11-01 / NULL | PENDING |
| Nhà trọ Bình An | Nước | NOT VERIFIED | 20,000 | m3 / usage | 2026-11-01 / NULL | PENDING |
| Nhà trọ Bình An | Internet | NOT VERIFIED | 100,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |
| Nhà trọ Bình An | Rác | NOT VERIFIED | 30,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |
| Khu trọ Sunrise | Điện | NOT VERIFIED | 3,800 | kwh / usage | 2026-11-01 / NULL | PENDING |
| Khu trọ Sunrise | Nước | NOT VERIFIED | 22,000 | m3 / usage | 2026-11-01 / NULL | PENDING |
| Khu trọ Sunrise | Internet | NOT VERIFIED | 100,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |
| Khu trọ Sunrise | Rác | NOT VERIFIED | 30,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |
| Nhà trọ Minh Tâm | Điện | NOT VERIFIED | 3,600 | kwh / usage | 2026-11-01 / NULL | PENDING |
| Nhà trọ Minh Tâm | Nước | NOT VERIFIED | 20,000 | m3 / usage | 2026-11-01 / NULL | PENDING |
| Nhà trọ Minh Tâm | Internet | NOT VERIFIED | 90,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |
| Nhà trọ Minh Tâm | Rác | NOT VERIFIED | 30,000 | room_month / fixed_room | 2026-11-01 / NULL | PENDING |

All prices are proposal values in VND from the attached prompt, not current Services.Price, a contract, or an invoice rate. Exact ServiceId and catalog names must be resolved from live PostgreSQL after secure access. Missing services remain pending; do not create new services.

## Required before seed

1. Verify unique live PropertyId values and exact property names.
2. Review current Description, PropertyAmenities, and overlapping PropertyServiceRates.
3. Resolve actual AmenityId / ServiceId, unit mapping, effective dates, and conflicts.
4. Obtain owner approval for each exact description, amenity assignment, and rate row.
5. Rehearse the final idempotent SQL twice on an isolated migrated fixture.

Until all five pass, Gate F remains NOT STARTED.