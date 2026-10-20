# 04 — Isolated replay and backend regression

**Status: Migration-focused PostgreSQL tests PASS; separate public-listing fixture tests remain SKIPPED.**

## Fresh run

- Created a temporary PostgreSQL 17.10 cluster bound only to 127.0.0.1:55432, with synthetic test data and no Supabase connection string.
- Ran dotnet test PRN232_BE.sln --no-restore --verbosity minimal with the migration-test variable pointing only to that loopback cluster.
- Result: 199 passed, 0 failed, 4 skipped, 203 total. Build completed as part of the test run.
- The Phase 3.1B migration/data-invariant test (including its live-like Gate B path) and both Phase 3.1C PostgreSQL tests ran; no test failed.
- Four PublicPropertyListingPostgreSqlTests were skipped because their separate PRN232_TEST_POSTGRES_CONNECTION fixture was not supplied. The migration fixture variable is a different setting.
- The temporary server was stopped, port 55432 was confirmed closed, and its synthetic data directory was removed.

The existing Phase 3.1C report records a prior full run of 203 passed / 0 skipped / 0 failed, including 50 Phase 3.1C API/PostgreSQL checks. See outputs/phase3-1c-dev-public-property-enrichment/01-Executive-Summary-And-Development-Status.md and 06-PostgreSQL-Provider-Real-Test-Matrix.md.

## Limits

The local replay is synthetic PostgreSQL 17.10 under a local superuser. It does not prove Supabase's 17.11 role/extension privileges, live backup recovery, current schema/history, or production API state. The local replay and tests did not write to Supabase.