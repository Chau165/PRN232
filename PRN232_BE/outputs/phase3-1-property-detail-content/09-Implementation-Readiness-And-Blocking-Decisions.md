# Phase 3.1 — Readiness and production blockers

## Implemented in source

The public property detail page now restores the three requested sections. Amenities are sourced from the current property’s rentable rooms and include per-amenity room coverage. Introduction and cost sections accurately state when data is unavailable. BE and FE automated verification results are recorded in `05-Test-Matrix-And-UAT.md`.

## Production blockers / owner decisions

1. **Introduction:** live schema has no description field and no verified copy was supplied. Approve the nullable field and provide owner-verified content before migration/backfill.
2. **Property amenities:** current UI presents derived rentable-room amenities. Decide whether this is the intended public meaning or whether managers need an explicit property-level amenity list and owner-scoped editing.
3. **Living costs:** current schema has no structured utility unit/billing basis or property-scoped tariff. Choose the global or property/room-type rate model, define semantics, and provide verified values. Do not publish current `Services.Price` as a property cost by inference.
4. **HTTP/browser UAT:** not completed against a freshly started API with the new response. Existing local API returns the old DTO; temporary server launch was rejected by tool policy.
5. **Existing image assertions:** two full BE PostgreSQL tests fail because live `PrimaryImageUrl` values are non-null while the tests expect null. Track/resolve separately before using the full suite as a clean production gate.
6. **Runtime parity:** FE checks used Node 26.7.0 although repository configuration specifies Node 22; repeat the FE verification on the pinned version.

## Database/live state

No migration, schema change, or live row update occurred. The recommended next step is for the owner to answer the four data-model/content decisions in `02-Data-Model-Proposal-And-Owner-Decisions.md`. After approval, prepare a reviewable migration and verified initial values; execute only after separate explicit approval.

## Release assessment

**Source implementation:** ready for review.

**Database-backed introduction and living costs:** blocked on approved schema and verified data.

**Browser end-to-end / production sign-off:** not verified; do not treat this as completed UAT.
