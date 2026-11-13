# Contract meter handover baseline — B1

Date: 13 November 2026 (Asia/Ho_Chi_Minh).

## Implementation and source

- Audited local source at `0ce934d6fbb5992828d4af03f16932b5c94413da`, matching the supplied GitHub reference. The initial worktree was clean. No reset, stash, commit, push or application deployment was performed. Final worktree changes are unstaged task changes/new files. A detached temporary comparison worktree reproduced pre-existing test failures.
- Kept the existing Domain → Application → Infrastructure/API architecture, repository Entity/ReadModel boundaries, controller conventions and existing DI registrations.
- `Contracts` gains nullable `InitialElectricReading`, `InitialWaterReading`, `MeterHandoverDate`. Existing contracts remain null; no historical handover is inferred.
- Contract read models and response DTOs append the three nullable values and derived history flags. `hasMeterHandover` derives from all three values. Existing contract, credit and timeline fields are preserved.
- New route: `PUT /api/contracts/{id}/meter-handover`, on the existing controller, authenticated and restricted to Manager. The service also verifies role; the database verifies active user, current Manager role and property assignment. Admin and Customer can read within their existing scopes and cannot write.
- Request fields: `meterHandoverDate`, `initialElectricReading`, `initialWaterReading`. Corrections must additionally send `expectedMeterHandover` containing the previously observed triple. An initial request sends null/omits the expected triple.
- Missing fields/invalid dates or values return 400; unauthenticated 401; unauthorized 403; missing contract 404; locked/stale changes 409; success 200 with the existing ContractDto extended as above. Raw database errors are not returned by this endpoint.
- The service validates nonnegative values, Active status, handover date within StartDate/EndDate and no later than today in Vietnam. Handover after StartDate is valid.
- Existing `UtilityReadings` columns are `numeric(18,2)` on both source and Live. Baseline and final-reading inputs must fit that precision (16 integer digits, two fractional digits); values are rejected rather than silently rounded. New baseline columns themselves remain unconstrained `numeric`, as requested.

## Transactions and history protection

- Baseline writes take a PostgreSQL `FOR UPDATE` lock on the contract row. They refresh tracked state after locking and recheck contract state/dates, active manager/role and assignment inside the transaction. Existing assignment/user shared-lock helpers protect the new handover operation.
- Updates compare the expected triple under the row lock. Two different updates from the same observed baseline cannot both win. Repeating identical values is idempotent, including after finalization, and performs no business-data update.
- Any existing reading or invoice prevents adding/changing a baseline. No reading, invoice, payment or room-status change is created by handover. Corrections remain possible before finalization and are logged using the existing logging infrastructure.
- Utility-reading creation retains its existing contract-row and assignment locks. It rechecks current Active status, assignment, month sequence, baseline/latest reading, start/end values and absence of a conflicting issued invoice under that lock.
- The first reading requires a complete valid baseline and the contract's starting calendar month. Legacy request start fields cannot bypass a missing baseline or override it.
- Later readings inherit the latest ends, including for legacy contracts with no baseline. Reading months must be consecutive. Existing readings are never overwritten or recalculated.
- Kept the existing policy for user-role/active changes after the last utility-reading authorization check: an already validated write may finish while a new request is denied. Assignment revocation remains serialized by its shared lock. This task did not broaden that existing policy across unrelated operations.

## Frontend

- Shared `MeterHandoverModal`: property, room, customer and start date; only the three handover inputs; date/nonnegative/precision checks; correction confirmation; duplicate-submit protection; API error states; keyboard focus handling. Success is reported only after saving and refetching the contract.
- ManagementContractsPage shows creation/correction actions only for Manager + Active + confirmed absence of readings/invoices. Details display the stored baseline or a truthful missing/legacy state.
- CreateInvoiceModal reads contract baseline and existing readings. Starting values are read only; only ending values are submitted. Missing baseline opens the same handover form. Loading/error/retry and sequence checks prevent treating an unloaded history as an empty first period. Existing monthly readings are displayed without a save action.
- Fixed-service invoice preview continues to use the existing server eligibility rules. Saving a baseline never creates an invoice.
- MyContracts displays the same read-only handover section. Legacy readings can be viewed without a fabricated baseline. Existing billing groups, extension/termination, invoice and payment actions are retained.
- No duplicate controller or utility API service, hardcoded contract/manager IDs, mock production data or new frontend dependency was introduced.

## EF migration, backup and rehearsal

- Migration: `20261113132725_AddContractMeterHandoverBaseline`, generated using EF CLI 8.0.10 and the existing Infrastructure/API projects. Designer and model snapshot are generated and synchronized.
- Up contains exactly three nullable AddColumn operations on Contracts: two `numeric`, one `date`; no defaults, backfill, other table changes or optional CHECK. Down removes only those columns.
- `migration.sql` is the EF-generated idempotent script for the exact range `20261113103807_SecurePaymentTables` → the new migration. Its three ALTER statements and single EF history insert execute in one transaction. Supabase's separate migration mechanism is not also invoked.
- Verified project: `qvdrjyecaprfpgvbfyfe`, PRN232_DB, database postgres, public schema. Preflight found all 25 local predecessor migrations applied and exactly this one pending, with no unknown migration or pre-existing baseline column.
- Full custom-format database backup (outside the Git worktree): `C:\Users\Surface\Documents\PRN232-private-backups\PRN232_DB_pre_meter_handover_20261113.dump`.
- Backup size: 684,194 bytes. SHA-256: `5CCBB2571CA81264FC44A9EAAE16EC29A4858648AD770AEBBB05E6E4AAA4AB5E`.
- `pg_restore --list` succeeded (1,202 TOC lines); `pg_restore --file=NUL --no-owner --no-acl` decoded the entire archive successfully. The public schema and its data were also restored successfully to the dedicated PostgreSQL 17 loopback instance at port 55432. Supabase internal schemas were not claimed to have been restored to vanilla PostgreSQL.
- Applied the exact generated script to the restored public schema: 25 → 26 migrations; all seven business-table counts and content hashes unchanged; all pre-existing public column definitions unchanged. Timestamptz hashes are normalized to UTC in `inspect.sql` before comparing Supabase with the local restore.
- Guarded rollback and reapply succeeded on that clone with empty baseline columns. The rollback script refuses to run if any baseline field is populated. Prefer reverting the application while retaining additive columns after any genuine handover has been recorded.

## Validation evidence

- TDD RED: the first 14 new API cases failed before implementation (missing route / old start-value bypass). Updated invoice UI expectations initially failed before its implementation.
- Focused PostgreSQL/source regression: 66 passed before the final precision guard. Includes actual PostgreSQL row locking, concurrent baseline updates, stale reading after a baseline edit, duplicate readings, status changes, assignment revocation, legacy preservation and historical migration coverage.
- An old migration fixture now inserts a historical contract using the schema available at that migration; it no longer tries to insert today's new columns into an old schema. Reading concurrency fixtures now have a real test baseline and no already-issued invoice. Their original concurrency assertions are retained.
- Acceptance fixture proves 1,200 → 1,248 kWh and 45 → 49 m³, with existing rate/credit logic: 168,000 electricity + 80,000 water, prepaid rent credit 3,000,000, remaining payable 248,000 when fixed services are absent. Preview/create totals agree and duplicate invoice creation is rejected.
- Full backend Release run before the final precision guard: 1,429 passed, 5 failed, 6 skipped, 1,440 total. Restore/build passed. Three failures reproduce at unmodified HEAD: AssignmentNoteMigrationTests has a missing Contract FK fixture; PaymentTableSecurityMigrationTests expects a permission error not raised; Phase31C legacy property-detail test returns a different status. A fourth is the existing read-only Supabase inspection's missing backup/approval flags; no dummy flags were set. A fifth was an isolated PostgreSQL cleanup/connection timeout in Duplicate_invoice_payment; the concurrency class passed in the separate 66-case run.
- Full frontend run: 42 files / 318 Vitest tests plus 2 Node tests passed; TypeScript and Vite production build passed. Initial simultaneous large test runs encountered Windows resource failures; the successful frontend run was sequential.
- Final focused backend Release validation after precision guard: **300 passed, 0 failed, 1 skipped**. Includes contract API/service/repository, utility readings, invoice/credit/payment-link/webhook and actual PostgreSQL concurrency. The skipped case is `PayOSFrontendEndToEndTests.Frontend_sdk_webhook_database_and_reports_agree`, which needs its external E2E environment.
- Measured coverage for the five affected backend policy/service/repository classes: **96.33% lines, 82.92% branches**. UtilityReadingPolicy line coverage is 100%. This is affected-code coverage, not repository-wide coverage.
- Final affected frontend tests after precision/keyboard/retry additions: **32 passed** across three files. Coverage for the handover modal, shared details and invoice modal: **87.78% statements, 86.60% branches, 85% functions, 90.10% lines**, passing existing thresholds. Final TypeScript check and production build passed.
- The successful full frontend run preceded the last precision/keyboard additions; those additions are covered by the separate 32-case run. No test or coverage threshold was disabled.

## Live status and deployment

Live application deployment and production API writes are outside this database-only release. No demo POST/PUT is used against Live. Deploy the compatible backend after the schema migration, then deploy the frontend. Contract #1 still requires a real manager-confirmed handover before its first usage reading; Contract #2 retains its existing historical reading/invoice without a baseline.

**IMPLEMENTED: YES. TESTED: YES (with the full-suite limitations above). LIVE MIGRATION APPLIED: NO — authentication blocker.**

Final pooler preflight failed with `password authentication failed for user "postgres"`; a second read-only attempt reproduced it. Neither attempt reached the migration script. No credential was changed; the cause of the authentication failure was not established.

Supabase MCP still works for read-only inspection. Its final query confirmed **25 EF migrations and zero baseline columns**, with all seven business hashes/counts and the pre-existing schema unchanged. `live-inspection-no-migration.json` contains only counts, hashes, migration metadata and schema state, not business row contents. Live was not modified.

Supabase's own schema_migrations table is absent. The apply_migration fallback was not invoked because introducing a separate tracking schema/table would exceed the three-column-only scope. No authenticated Dashboard session was available. The next required input is a refreshed authorized database connection in the Codex environment, or an authenticated Dashboard SQL editor session for the reviewed EF script. Do not paste credentials into chat.

**Do not start/deploy the new backend against Live until this migration is applied.** Production API requests with the new implementation and deployed frontend browser behavior remain unverified. The task is incomplete at the Live migration/post-migration verification step.

| Table | Before | After |
| --- | ---: | ---: |
| Contracts | 2 | 2 (not migrated) |
| UtilityReadings | 1 | 1 (not migrated) |
| Invoices | 1 | 1 (not migrated) |
| InvoiceDetails | 8 | 8 (not migrated) |
| Payments | 4 | 4 (not migrated) |
| Bookings | 8 | 8 (not migrated) |
| PropertyManagerAssignments | 30 | 30 (not migrated) |
| __EFMigrationsHistory | 25 | 25 (not migrated) |

Official references used for the release approach: [EF Core migration scripts](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [Supabase database restore scope](https://supabase.com/docs/guides/self-hosting/restore-from-platform).
