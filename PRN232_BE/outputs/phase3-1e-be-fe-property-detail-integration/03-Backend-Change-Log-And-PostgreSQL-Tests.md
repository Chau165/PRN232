# Backend change log and PostgreSQL tests

## Changes

- `PRN232.Application/Services/PublicPropertyListingService.cs`: calculate Vietnam local month start rather than local day for service-rate filtering.
- `PRN232.Application/Interfaces/IPublicPropertyListingRepository.cs` and `PRN232.Infrastructure/Repositories/PublicPropertyListingRepository.cs`: rename the date argument to `currentMonthStart`; keep repository queries and `[from,to)` semantics.
- `PRN232.Application.Tests/PublicPropertyListingServiceTests.cs` (new): captures the date passed to the repository across the October 31 Vietnam month boundary.

No controller, DTO shape, room-availability query, migration, or database write was added.

## TDD evidence

- RED: the new service test at UTC `2026-10-31 16:59` expected `2026-10-01` but the old implementation passed `2026-10-31`; the corresponding 17:00 UTC case correctly crossed to November.
- GREEN: the same tests pass after deriving the first local calendar day.
- An initial attempted mid-month rate fixture was rejected by migrated PostgreSQL's `CK_PropertyServiceRates_EffectiveFrom_MonthStart`; that invalid row was removed. The final migrated fixture uses legal month-start periods and still checks the half-open November boundary.

## PostgreSQL evidence

- Ran `Phase31CPropertyDetailPostgreSqlTests` against an isolated temporary PostgreSQL 17 loopback cluster: **2 passed, 0 failed**. This covers current migrated schema and the enrichment-off legacy-schema path.
- Ran the full BE test project against the same isolated migration-test server: **201 passed, 4 skipped, 0 failed (205 total)**. Four older read-only tests require `PRN232_TEST_POSTGRES_CONNECTION`, which was not configured; the migrated Phase 3.1C fixture tests did run.
- The isolated test server on port 55432 was stopped after the run. Its task-specific data directory remains in `%TEMP%` because the recursive cleanup command was rejected by the local command guard.

A pre-existing `CS1998` warning in `RoleRepository.cs:35` was emitted during build; it is outside this phase.
