# API, DTO, and schema compatibility

## Existing contract retained

- Route remains anonymous `GET /api/public/properties/{id}`.
- Existing detail fields remain: property identity/name/address, image, available-room count, starting rent, and `roomAmenities`.
- Existing public room-list and room-detail routes/DTOs were not changed. `RoomTypes.Price` remains starting rent; no living-cost value is added to it.
- Unknown or non-rentable properties continue to use the established 404 contract. Room availability continues through the existing Phase 2A query.

## Enrichment fields

The existing `PublicPropertyDetailDto` already had additive nullable/defaulted fields; the BE DTO was not rewritten:

| JSON field | Source | Empty behavior |
|---|---|---|
| `description` | `Properties.Description` | `null` remains null |
| `commonAmenities` | `PropertyAmenities → Amenities`, scoped to `PropertyId` | `[]` |
| `roomAmenities` | `RoomAmenities → Amenities` over Phase 2A rentable rooms | `[]` |
| `livingCosts` | `PropertyServiceRates → Services`, property-scoped | `[]` |

Schema verified on live: `Properties.Description` is nullable text; `PropertyAmenities` has `PropertyId`/`AmenityId`; `PropertyServiceRates` has `PropertyId`, `ServiceId`, `Price`, `UnitCode`, `BillingBasis`, `EffectiveFrom`, `EffectiveTo`. Existing month-start and effective-range constraints were preserved.

## Effective-date correction

With enrichment enabled, the service now converts the request's UTC instant to `Asia/Ho_Chi_Minh` and passes the first day of that local month to the repository. The repository keeps the half-open predicate:

`EffectiveFrom <= currentMonthStart && (EffectiveTo == null || currentMonthStart < EffectiveTo)`

This respects the database's month-start constraints and the contract's exclusive `EffectiveTo` boundary.

## Compatibility mode

`PublicBrowsing:PropertyDetailEnrichmentEnabled` remains opt-in and false when missing/invalid, preserving the pre-enrichment schema path. No runtime configuration was persisted. The local smoke process set this flag temporarily to verify the enriched API; deployment must explicitly configure it after its schema/runtime access is approved.
