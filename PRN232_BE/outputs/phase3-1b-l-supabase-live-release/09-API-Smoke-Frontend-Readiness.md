# 09 — API smoke and frontend readiness

**Status: Local API/PostgreSQL evidence exists; live API and frontend UAT are NOT VERIFIED.**

## API / tests

- The current test run passed the two Phase 3.1C PostgreSQL tests, including enriched property-detail behavior on a migrated isolated PostgreSQL database.
- Four PublicPropertyListingPostgreSqlTests for listing, property detail, property rooms, and room detail were skipped in the current run because PRN232_TEST_POSTGRES_CONNECTION was not configured. The Phase 3.1C report records a prior 203/203 full-suite pass and 50 Phase 3.1C API/PostgreSQL checks; this run did not repeat those four routes.
- No live API process was connected to a fresh migrated Supabase schema. No live GET result is claimed.

## FE

Current FE source includes roomAmenities, but the property-detail API type and page do not include/render description, commonAmenities, or livingCosts. The Phase 3.1C report also records that the FE remains on the placeholder state. Schema/API changes would therefore not make the new values visible in the current FE.

## Security / release

- Phase 3.1C enrichment remains feature-flagged off by default until the schema and data are approved.
- RolesController exposes POST /api/roles without an [Authorize] attribute. The Phase 3.1C security report also records legacy owner-scope and RLS/policy blockers.
- New tables with RLS and no approved policies are not automatically reachable through Supabase Data APIs.

**Security release remains BLOCKED.** No source deployment, FE change, or production-ready claim is made.