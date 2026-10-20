# 03 — Exact SQL hashes and live preflight

**Status: SQL hashes PASS; fresh live preflight BLOCKED.**

## Hash lock

SHA-256 was computed over the source files in the repository. All four match the attached manifest byte-for-byte.

| Gate | File | SHA-256 | Hash result |
|---|---|---|---|
| B | outputs/phase3-1b-r-migration-release-readiness/SQL_REVIEW/01-Gate-B-Maintenance-Reconciliation-REVIEW-ONLY.sql | d3dad4a20ab58aad4fbc374aa52a0508bd977f13ee5530475165d3104a1c0701 | MATCH |
| C | outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/01-AddPropertyDescriptionAndAmenities.sql | 7390649de04ed4e2b2220ac3b8ae0ac6a6e9f414feb41d3911ba51727d87d33a | MATCH |
| D | outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/02-AddPropertyServiceRates.sql | 8247c5208f02095cd288e4aa1c271b6e20f041a6c92712ccba3791ae759a509e | MATCH |
| E | outputs/phase3-1b-property-rates-invoice-snapshot/SQL_REVIEW/03-AddInvoiceDetailBillingSnapshot.sql | 225902a76a91ed7f548318fc60efeaf14204ed634d91266d6155992c9d6cbc85 | MATCH |

Expected EF migration IDs are B 20261013192052_ReconcileMaintenanceReportsAndLogNullability, C 20261019143543_AddPropertyDescriptionAndAmenities, D 20261019143615_AddPropertyServiceRates, and E 20261019143633_AddInvoiceDetailBillingSnapshot.

## Live preflight

00-Live-READ-ONLY-Preflight.sql was not run. VerifyFull certificate validation failed before SQL execution, so live schema, history rows, row counts, indexes, constraints, RLS, extension state, and data fingerprints remain NOT VERIFIED in this run. The earlier Phase 3.1B-R catalog snapshot is historical evidence and was not substituted for a fresh preflight.

No SQL file was edited, re-hashed, or executed on Supabase.