# Phase 3.1 — Data model proposal and owner decisions

**Status: proposal only. Nothing in this report was applied to the database or live data.**

## Introduction

Add a nullable description field to `Properties`, sized and constrained according to the existing live column conventions. Extend the existing owner-scoped property update DTO/service so only an authorized manager can edit it. Public reads should expose the stored value when non-empty; null/blank stays unavailable. Do not migrate mock copy into live rows without an owner-approved source for each property.

## Property amenities

The current `RoomAmenities` relation is room-scoped and already supports a safe public summary over rentable rooms. Keep this derived summary for Phase 3.1. If product requirements need an explicit property-level amenity claim independent of rooms, add a junction between `Properties` and `Amenities` using the exact existing key types, a composite primary key, and foreign keys. Add a manager/owner-scoped assignment flow before allowing edits. Do not bulk-copy room amenities into property-level claims: room coverage can differ, and a derived count is not an explicit property promise.

## Living costs / utility rates

Do not publish `Services.Price` as a current advertised rate until ownership, scope, unit, and billing period are confirmed. Two viable models:

1. **Globally uniform catalog rate:** retain `Services.Price`, add a structured unit/billing code and only use it publicly if the business owner confirms each service’s rate applies uniformly to every property. This has the smallest schema footprint but creates global pricing semantics.
2. **Property-scoped rates (recommended if rates vary by property):** add a rate relation keyed by property and service, with a non-negative amount, structured unit/billing code, and an owner-scoped management workflow. If rates can vary by room type, include a nullable room-type key plus a uniqueness rule for property defaults versus room-type overrides. Define precedence and effective-date/history behavior before migration. Do not silently fall back to `Services.Price`.

Either choice needs an explicit policy for included/free utilities, missing rates, currency, tax/rounding, and whether a displayed amount is a tariff or an estimate. Phase 2D room-detail output currently includes service price data; any change to its meaning must keep that endpoint’s contract and management flows consistent.

## Approval required before database work

Please decide:

- Should introduction text be added to each property, and who will provide/maintain the verified content?
- Are property amenities only a derived summary of rentable rooms, or should managers maintain explicit property-level amenities?
- Which rate model applies: globally uniform catalog rates or property/room-type-specific rates? Who confirms structured units, billing periods, currency, effective dates, and initial values?
- Should the Phase 2D room-detail service-price contract change if property-specific rates are adopted?

No migration SQL is presented as executable here because the ownership and rate semantics are not yet approved. After the decisions and initial verified values are supplied, the migration and backfill can be prepared for review before any live execution.
