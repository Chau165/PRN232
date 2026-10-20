# Phase 3.1B-L — Live Supabase Deployment Execution Report

## Result

All four owner-approved SQL files were applied to the configured Supabase PostgreSQL database. Postflight completed in a READ ONLY transaction with TLS certificate and host verification enabled (VerifyFull).

The database postflight timestamp was 2026-10-19 18:05:53.817988 UTC. No credentials or connection strings are included in this report.

## Approved SQL and execution result

| Gate | File | Approved SHA-256 | Result |
|---|---|---|---|
| B | 01-Gate-B-Maintenance-Reconciliation-REVIEW-ONLY.sql | d3dad4a20ab58aad4fbc374aa52a0508bd977f13ee5530475165d3104a1c0701 | Applied; reconcile history row present; both concurrent indexes valid and ready |
| C | 01-AddPropertyDescriptionAndAmenities.sql | 7390649de04ed4e2b2220ac3b8ae0ac6a6e9f414feb41d3911ba51727d87d33a | Applied and committed |
| D | 02-AddPropertyServiceRates.sql | 8247c5208f02095cd288e4aa1c271b6e20f041a6c92712ccba3791ae759a509e | Applied and committed |
| E | 03-AddInvoiceDetailBillingSnapshot.sql | 225902a76a91ed7f548318fc60efeaf14204ed634d91266d6155992c9d6cbc85 | Applied and committed |

Gate B ran statement-by-statement in autocommit mode, preserving the required behavior for CREATE INDEX CONCURRENTLY. Gates C, D, and E ran inside their authored explicit transactions. The local psql client was unavailable, so execution used the configured Npgsql connection with VerifyFull; TLS verification was not weakened.

## Postflight evidence

- EF migration history contains the original four migrations plus the Maintenance reconcile and Gates C, D, and E; all eight rows report EF product version 8.0.10.
- Existing row counts match preflight: Properties 3, InvoiceDetails 25, MaintenanceLogs 9, MaintenanceReports 4, MaintenanceReportImages 5.
- New tables are empty: PropertyAmenities 0 rows and PropertyServiceRates 0 rows. No sample amenity or rate data was added.
- All three Properties.Description values are NULL. No property descriptions were invented or backfilled.
- All 25 existing InvoiceDetails rows have the six new snapshot fields NULL. Existing invoice values were not updated.
- One MaintenanceLogs row has NULL OldStatus; the approved migration leaves it nullable and did not rewrite the row.
- MaintenanceReports.CreatedAt retains its CURRENT_TIMESTAMP default.
- btree_gist version 1.7 is installed in public. The no-overlapping-periods exclusion constraint is validated.
- The reviewed constraint set has 28 constraints, all validated. All 19 indexes across the affected tables are valid and ready.
- RLS is enabled, not forced, and has zero policies on MaintenanceLogs, MaintenanceReports, MaintenanceReportImages, PropertyAmenities, and PropertyServiceRates. This matches the approved SQL shape. The preflight connection role was postgres with BYPASSRLS; direct anon/authenticated access remains subject to the absence of policies.

## Waived or out-of-scope checks

Per the owner's explicit instruction, no new backup, restore drill, restore rehearsal, or full regression run was performed. The owner accepted the associated recovery risk.

No backend/frontend deployment, API smoke test, service restart, or billing-engine rollout was performed. Those remain a separate release step after database postflight acceptance. Whether application writers were paused during the migration window was not observable from this database session and remains unverified.

## Scope and repository state

Only the approved live database SQL was applied. No source code, FE files, or EF migration files were changed by this deployment. No files were staged, committed, pushed, or deployed from the repository.