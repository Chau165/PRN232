# Phase 3.1 — Live schema and code audit

**Date:** 2026-10-19

**Repositories:** `C:\PRN232\PRN232_BE`, `C:\PRN232\PRN232_FE`
**Scope:** Property detail introduction, amenities, and living costs

## Audit boundary

Reviewed the Phase 3.1 prompt, backend `CODING_STANDARDS.md`, Phase 2E report, Phase 2B–2D API contracts, relevant Phase 3 reports, the public property detail flow, its FE consumer, and the live PostgreSQL schema/data using read-only queries. CodeGraph was available for backend navigation; its index did not include the current untracked public-listing implementation, so source files were checked directly for that path. The FE has no CodeGraph index. This is a scoped call-chain audit, not a claim that every unrelated repository file was read.

No migration, `INSERT`, `UPDATE`, or `DELETE` was run against Supabase.

## Live schema findings

| Area | Observed schema/data | Phase 3.1 consequence |
|---|---|---|
| `Properties` | Property identity, owner, name, address/location, coordinates, nullable `PrimaryImageUrl`; no description/introduction column | No truthful introduction text can be returned. The UI shows a clear “đang được cập nhật” state. |
| `Amenities` | Shared amenity catalog with `AmenityId` and `Name` | Supplies labels, but does not say which property offers each amenity. |
| `RoomAmenities` | Room-to-amenity relation; no direct property relation | A property amenity summary can safely be derived from currently rentable rooms belonging to that property. |
| `Services` | Shared catalog with `ServiceId`, `Name`, `Price` | The catalog price has no structured unit/billing-period field and is not proven to be a property-specific public utility rate. |
| `RoomTypeServices` | Room-type-to-service relation | No property-specific rate or public cost contract. It is not safe to treat its association alone as a utility-cost statement. |
| Utility/invoice data | `UtilityReadings` contains meter readings, not a tariff; `InvoiceDetails` contains historical item description/amount without a service identity suitable for current advertised rates | Neither provides a current, comparable living-cost estimate. |
| Property-level amenity/rate tables | No `PropertyAmenities`, `PropertyServices`, `PropertyServiceRates`, or `UtilityRates` table found | Persisted property-level amenities and advertised rates need an approved model and owner-managed data path. |

The `Services` names in the live catalog include “Electricity - per kWh”, “Water - per m3”, “Internet - monthly”, and “Trash - monthly”. Those are display names, not structured unit metadata; no unit or billing rule is inferred from their text and no price is displayed as a verified public cost.

## Existing code path

- Anonymous public detail route: `GET /api/public/properties/{id}`.
- Backend detail already calculates available room count and starting rent using the public rentability rule. The Phase 3.1 change derives amenity summaries from those same rentable rooms.
- The query is property-scoped and grouped in SQL; it does not return owner data or use per-room N+1 queries.
- The FE public detail page consumes the detail endpoint. Previously removed mock copy/amenities/costs were not restored.
- Existing property update authorization is Admin/Manager with owner-scope checks, but its DTO has no description field. There is no owner-scoped property-amenity assignment flow. Existing service administration is global and does not provide an approved property-specific rate workflow.

## Read-only data snapshot

Snapshot date: **2026-10-19 (Asia/Saigon)**. Counts use the same current rentable-room predicate as the public detail query; they are a time-bound observation and can change with booking/room state.

| Property | Rentable rooms | Starting rent | Amenities present on rentable rooms |
|---|---:|---:|---|
| ID 1 — Nhà trọ Bình An | 1 | 3,500,000 | Wi-Fi 1, Air conditioner 1, Balcony 1, Private kitchen 1 |
| ID 2 — Khu trọ Sunrise | 2 | 2,500,000 | Wi-Fi 2, Air conditioner 1, Balcony 1, Private kitchen 1, Shared washing machine 1 |
| ID 3 — Nhà trọ Minh Tâm | 1 | 3,500,000 | Wi-Fi 1, Air conditioner 1, Balcony 1 |

The detail query test also confirms an amenity attached only to a room blocked by a pending booking is excluded from that property’s public rentable-room summary.

## Current gaps

1. No stored property description exists.
2. No explicit property-to-amenity mapping exists. The implemented public summary is intentionally a derived “amenities available among rentable rooms” summary, not a claim that every room has each amenity.
3. No structured, property-scoped utility tariff with unit/billing basis exists. Living costs remain an explicit unavailable state.
4. Adding persistent content requires schema work and a corresponding owner-authorized write path; neither is part of this unapproved database scope.
