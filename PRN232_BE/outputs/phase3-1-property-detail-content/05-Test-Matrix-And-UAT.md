# Phase 3.1 — Test matrix and UAT status

## TDD sequence

Added failing tests first for the missing property-detail sections and missing `roomAmenities` API contract, then implemented the FE and BE changes and reran the targeted tests.

| Area | Verification | Result |
|---|---|---|
| FE property detail | Vitest tests for headings/unavailable states, actual API amenities, property isolation, coverage counts, empty state, known amenity labels | **9/9 passed** |
| FE full suite + focused coverage | Vitest: 3 files, 28 tests; coverage focused on `PropertyDetailPage.tsx` | **28/28 passed**; statements 100%, branches 88.05%, functions 100%, lines 100% |
| FE type check | `tsc --noEmit` | **Passed** |
| FE production build | Vite build | **Passed**; emitted Node `DEP0205` deprecation warning (`module.register`) |
| FE management contract regression | Node test runner, `managementContracts.test.cjs` | **6/6 passed** |
| BE targeted detail tests | Public property detail unit/API and PostgreSQL detail tests | **4 passed, 0 skipped** |
| BE complete suite against read-only PostgreSQL | Full application test project | **197 passed, 2 failed (199 total)**; both failures are existing `PrimaryImageUrl` null expectations contradicted by current live rows containing Supabase public URLs |
| BE suite excluding the two image-contract failures | Same suite, no rebuild | **197/197 passed** |

The two full-suite failures are `Four_public_browsing_routes_work_over_read_only_postgresql_without_authentication` (assertion at `PublicPropertyListingPostgreSqlTests.cs:302`) and `Listing_query_executes_on_postgresql_with_a_read_only_session` (assertion at line 65). They expect `PrimaryImageUrl` to be null while the live records have non-null public object URLs. This Phase 3.1 change did not alter image handling; these failures are documented as live data/test-contract drift and should be resolved in their own approved scope.

FE verification used the installed local Node **v26.7.0** runtime; the repository declares Node 22. Node 22 execution was not verified. `pnpm exec` was not used for final verification because it attempted to manage the existing modules directory and aborted without a TTY; no dependency install occurred.

## Live database safety

Schema/data checks and PostgreSQL tests ran using read-only database sessions/queries. No DDL/DML or migration was executed. The live data snapshot is documented in `01-Schema-And-Code-Audit.md`.

## UAT not verified

- No browser session exercised the changed FE against a freshly started Phase 3.1 API.
- The existing API on port 5102 serves the previous DTO shape, so its response cannot prove the new field over HTTP.
- Starting temporary local API/Vite servers was rejected by the execution/tool policy. No workaround was attempted, and the existing server was not stopped or changed.
- Node 22 verification remains open.

These are verification limits, not evidence that the new browser flow passed. Complete browser UAT with the API response containing `roomAmenities` before production sign-off.
