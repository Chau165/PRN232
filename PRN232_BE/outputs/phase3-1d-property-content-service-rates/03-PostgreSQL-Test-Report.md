# Phase 3.1D — PostgreSQL seed test report

## Isolated PostgreSQL test

PASS on a task-created PostgreSQL 17.10 instance bound only to 127.0.0.1:55432. The database was separate from Supabase and the Windows PostgreSQL service. Eight EF migrations were applied to the temporary test database; the latest was 20261019143633_AddInvoiceDetailBillingSnapshot.

The two focused Phase31CPropertyDetailPostgreSqlTests passed (2 passed, 0 failed, 0 skipped). They exercise the enriched Property Detail endpoint with migrated PostgreSQL and verify feature-off compatibility against the pre-enrichment schema.

The reviewed seed SQL was run twice on synthetic fixture data:

| Check | First run | Second run |
|---|---:|---:|
| Property descriptions updated | 3 | 0 |
| Property amenity links inserted | 3 | 0 |
| Property service rates inserted | 12 | 0 |

All rates satisfied the migrated PostgreSQL constraints. The four fixture Services.Price values remained unchanged. The temporary PostgreSQL instance and its cluster directory were stopped and removed.

## Supabase live follow-up

After the owner approved the exact SQL SHA-256, the live preflight, seed, postflight and API smoke test completed. The actual inserted live rows and API results are recorded in 04-Live-Write-Report.md.
