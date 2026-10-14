# Maintenance port and TDD evidence — 2026-10-14

Source: `P:\PRN232\PRN232_BE` (read-only reference).
Target: `C:\PRN232\PRN232_BE` (all final code changes).
Specification: the attached “Port the completed Maintenance + Maintenance Report backend flow” task. Journeys and tests were derived from that specification, the reference implementation, and the target's existing tests.

**Local implementation and verification: PASS. Live readiness: BLOCKED by the two concrete issues below.**

- Live `MaintenanceLogs.OldStatus` is **NOT NULL**, while creation must persist SQL NULL → Pending. A new customer request therefore cannot succeed against the audited live schema until its nullability is corrected separately. `maintenance-log-old-status.review.sql` is prepared for review only and was NOT applied.
- The inspected target development configuration, process environment, and launch profiles have no configured `Supabase:Url`, `Supabase:ServiceRoleKey`, or `Supabase:MaintenanceReportBucket`. Live uploads/signing need these server settings. No credential values were copied, requested in chat, or set.

No live writes, storage uploads, seeding, migrations, commits, pushes, deployment, or reference-repository edits were performed. Test fixtures use disposable SQLite/InMemory databases and mocked Storage HTTP calls.

## Comparison and port decisions

| Area | Source P:\ | Initial target C:\ | Decision |
|---|---|---|---|
| Maintenance create/read | JWT ownership and role scope | CRUD, client customer filter, anonymous reads | Port authenticated ownership; retain valid contract/equipment checks |
| Admin assignment | Active Manager only; automatic Approved | Client status; active-user check | Assignment-only DTO; server-controlled status and JWT actor |
| Lifecycle | Pending → Approved → Completed | Included Rejected/InProgress paths | Only the three final persisted statuses; obsolete positive tests updated |
| Logs | Initial, assignment, completion | Transition logs, nullable actor; no initial log | Atomic persistence and authenticated actors for all three events |
| Reports | Multipart, Manager ownership, metadata, images | Absent | Port report flow, DTOs, entities, storage adapter, repository operations |
| Report reads | Admin only, dynamically signed images | Absent | Port Admin endpoint and stable object-path storage |
| Authentication | Users registration / Sessions login, separate token service | AuthController login/register, JwtSettings, JwtTokenGenerator, existing user management | Target Auth remains authoritative; source Auth not copied |
| Current user | Mapped NameIdentifier/Email claims | Controllers read `sub` directly, JWT mapping disabled | New adapter reads existing target principal; no second JWT validator or issuer |
| Program.cs | Source-specific Auth/DI/Swagger | More teammate module registrations | Merge only current-user/storage DI and authorization-aware Swagger filter |
| AppDbContext | Report mappings; different Users mapping | Unique Users.Email plus other team mappings | Merge report mappings only; preserve Users.Email uniqueness and unrelated mappings |
| Approval validation | Does not revalidate contract/equipment | Existing tests enforce revalidation | Retain target's existing guarantees; validate date and equipment before assignment commits |
| Atomic completion | Transaction with tracked status update | No completion reports | Conditional update checks Approved AND AssignedTo under the report transaction |
| Tests | Maintenance, storage, source-specific Auth/API tests | 36 existing Maintenance cases | Port Maintenance/storage cases; write target-specific HTTP tests; retain all 27 original service test method names |

Files compared include Maintenance controller, DTOs, interfaces, service/status constants, repository, domain entities, storage, AppDbContext, Program.cs, package manifests, migration snapshot/history, Maintenance tests and testing docs in both repositories. Auth/User DTOs, interfaces, services, controllers, JWT generation/validation and DI were compared as required to determine integration points. CodeGraph was consulted first; the target query reported its index unavailable, so target decisions were verified directly from source.

Almost-direct ports: Maintenance DTOs/interfaces, status and role constants, report entities, storage adapter, Maintenance tests. Manual adaptations: controller wiring, target current-user adapter, Program.cs, AppDbContext, entity navigations, assignment validation, atomic repository completion, target-specific API tests and configuration examples.

Intentionally not copied: source `AuthService`, `UserService`, `UsersController`, `SessionsController`, Auth/User DTOs and interfaces, password/JWT implementations, complete `Program.cs`/`AppDbContext` files, appsettings, credentials, source migrations or model snapshot, source Auth/API tests, Swagger tag ordering and unrelated features.

## Authentication preserved

- `/api/auth/register` and `/api/auth/login`, their request/response DTOs, `AuthService`, `UserService`, repositories, password hasher, `JwtTokenGenerator`, `JwtSettings`, middleware validation and login rate limiter remain the target implementations.
- The actual unchanged generator emits `sub`, `email`, and URI claim types for Name and Role. Its own token was decoded in a test and `ClaimsPrincipal.IsInRole("Manager")` verified. Existing `MapInboundClaims = false` and JWT validation configuration remain unchanged.
- `CurrentUserService` reads `sub` for UserId, `email` for Email and the existing Name/Role claim types from `IHttpContextAccessor.HttpContext.User`. Missing, invalid or nonpositive UserId cannot supply an actor. Claim mapping and role interpretation were checked against [Microsoft's claims guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims?view=aspnetcore-7.0).
- One existing token generator and one existing JWT validation flow remain in effect. Maintenance uses their authenticated principal. No parallel login/register routes were added.
- HTTP regression tests verify target registration/login, Customer default role, password hashing, normalized email, duplicate registration, invalid login, inactive login, `/api/users/me`, Admin `/api/users`, role denial and invalid JWT denial.
- Hash verification: all 23 protected Auth/config/migration baseline files remained byte-identical. Program.cs's JWT configuration/validation block was preserved. Users gained only a report navigation property.
- Swagger retains the target Bearer scheme and input style. Security requirements now attach to authorized operations; public login/register are no longer shown as requiring Bearer authentication.

## Created files in C:\ (16)

| File relative to target | Purpose |
|---|---|
| `PRN232.Api/Security/CurrentUserService.cs` | Adapter for target JWT claims |
| `PRN232.Api/Security/AuthorizeCheckOperationFilter.cs` | Swagger authorization metadata |
| `PRN232.Application/Interfaces/ICurrentUserService.cs` | Current principal contract |
| `PRN232.Application/Interfaces/IMaintenanceReportImageStorage.cs` | Upload/sign/delete abstraction |
| `PRN232.Application/Services/RoleNames.cs` | Customer/Manager/Admin constants |
| `PRN232.Domain/Entities/MaintenanceReports.cs` | Report POCO |
| `PRN232.Domain/Entities/MaintenanceReportImages.cs` | Evidence metadata POCO |
| `PRN232.Infrastructure/Storage/SupabaseMaintenanceReportImageStorage.cs` | Private Storage HTTP adapter |
| `PRN232.Application.Tests/MaintenanceApiTests.cs` | Anonymous read RED/green tests |
| `PRN232.Application.Tests/MaintenanceCurrentUserTests.cs` | Actual target JWT/current-user contract |
| `PRN232.Application.Tests/MaintenanceFlowApiTests.cs` | HTTP/Auth/ownership/lifecycle/Swagger tests |
| `PRN232.Application.Tests/MaintenanceTransactionTests.cs` | Relational rollback, unique report and atomic assignment checks |
| `PRN232.Application.Tests/SupabaseMaintenanceReportImageStorageTests.cs` | Mocked Storage protocol/config/failure tests |
| `PRN232.Application.Tests/Maintenance.runsettings` | Coverage scope including async state machines |
| `docs/testing/maintenance-log-old-status.review.sql` | Unapplied correction for the separate schema owner |
| `docs/testing/maintenance-port.tdd.md` | This comparison, migration report and evidence index |

## Modified files in C:\ (17)

| File relative to target | Change |
|---|---|
| `PRN232.Api/Controllers/MaintenanceRequestsController.cs` | Auth, ownership-aware calls, assignment-only PATCH, multipart reports, Admin report read, errors |
| `PRN232.Api/Program.cs` | Current-user/storage registrations and Swagger operation filter; existing team registrations retained |
| `PRN232.Application/DTOs/MaintenanceRequestDto.cs` | Assignment-only input; nullable initial OldStatus; report and image DTOs |
| `PRN232.Application/Interfaces/IMaintenanceService.cs` | Authenticated identity/role and report methods |
| `PRN232.Application/Interfaces/IMaintenanceRepository.cs` | Scoped reads and atomic lifecycle/report persistence |
| `PRN232.Application/Services/MaintenanceService.cs` | Full authorized lifecycle, validation, stable paths, signed reads and best-effort cleanup |
| `PRN232.Application/Services/MaintenanceRequestStatuses.cs` | Exactly Pending, Approved, Completed |
| `PRN232.Infrastructure/Repositories/MaintenanceRepository.cs` | EF projections, scope, transactions, report queries and conditional completion |
| `PRN232.Infrastructure/Data/AppDbContext.cs` | Two DbSets/keys, request/report 1:0..1, unique RequestId, reporter FK, image/report 1:N |
| `PRN232.Domain/Entities/MaintenanceRequests.cs` | Optional Report navigation |
| `PRN232.Domain/Entities/MaintenanceLogs.cs` | Nullable OldStatus for initial lifecycle event |
| `PRN232.Domain/Entities/Users.cs` | Report navigation only; Auth fields retained |
| `PRN232.Application.Tests/MaintenanceServiceTests.cs` | Ported service cases; original test names/valid guarantees preserved; obsolete transitions updated |
| `PRN232.Application.Tests/MaintenanceRepositoryTests.cs` | Original customer projection test retained; Manager assignment scope/filter bypass test added |
| `PRN232.Application.Tests/PRN232.Application.Tests.csproj` | API reference, MVC TestServer and SQLite packages |
| `.env.example` | Empty URL/key placeholders and bucket example only |
| `docker-compose.yml` | Pass the three new Storage variables to the API container |

No production package was added or downgraded. Test-only additions: `Microsoft.AspNetCore.Mvc.Testing 8.0.10`, `Microsoft.EntityFrameworkCore.Sqlite 8.0.10`. Existing package versions were retained.

## Routes and authorization

| Verb | Route | Rule |
|---|---|---|
| GET | `/api/maintenance-requests` | Customer: Contract.CustomerId == JWT UserId; Manager: AssignedTo == JWT UserId; Admin: all. status/assignedTo filters cannot widen scope |
| POST | `/api/maintenance-requests` | Customer only, own valid contract; server sets Pending and actor |
| GET | `/api/maintenance-requests/{id}` | Same ownership scope; unrelated authenticated users get 403 |
| PATCH | `/api/maintenance-requests/{id}` | Admin only, Pending request, active Manager assignment; server sets Approved |
| GET | `/api/maintenance-requests/{id}/logs` | Same ownership scope |
| POST | `/api/maintenance-requests/{id}/reports` | Assigned Manager only; Approved; multipart/form-data |
| GET | `/api/maintenance-requests/{id}/reports` | Admin only; 404 if no report |

Anonymous calls return 401. Wrong role/ownership returns 403. Invalid report/input/filter returns 400. Missing resources return 404. Invalid/duplicate completion returns 409. Storage HTTP failures return a sanitized 502.

Create input has ContractId, optional EquipmentId, Description. PATCH input contains only AssignedTo. Multipart report input exposes only Description and Images. Spoofed CustomerId/ChangedBy/ReportedBy/ManagerId/Status values do not determine actors or lifecycle state.

## Lifecycle, reports and Storage

| Event | OldStatus | NewStatus | ChangedBy |
|---|---|---|---|
| Customer creates | SQL NULL | Pending | Customer JWT UserId |
| Admin assigns | Pending | Approved | Admin JWT UserId |
| Assigned Manager reports | Approved | Completed | Manager JWT UserId |

Every event commits request changes and its log together. Completion also commits the report and image metadata in that transaction. A conditional Approved + AssignedTo update protects the persistence boundary. Approved/Completed cannot be reassigned through PATCH. Admin cannot complete through arbitrary status input.

Reports require nonblank Description (at most 4000 characters) and 1–5 images, at most 5 MiB each. MIME/signature checks allow JPEG/PNG/WebP; empty, oversized, unsupported and spoofed evidence is rejected. HTTP body limit is 26 MiB. Storage assigns random server object paths; filenames do not control paths. DB stores these stable paths, not temporary URLs. The private-bucket read design follows [Supabase's bucket documentation](https://supabase.com/docs/guides/storage/buckets/fundamentals) and [signed URL API](https://supabase.com/docs/reference/javascript/file-buckets-createsignedurl).

Admin report responses include ReportId, RequestId, Description, ReportedBy, ReportedByName, CreatedAt and image metadata with dynamically generated read URLs. Signed URLs last 3600 seconds. The audited bucket is private. Upload/DB failure does not complete the request; previously uploaded objects receive best-effort cleanup if persistence fails. A signing failure after DB commit preserves committed evidence and allows a later Admin read retry.

## Configuration and database evidence

| Environment variable | .NET key |
|---|---|
| `Supabase__Url` | `Supabase:Url` |
| `Supabase__ServiceRoleKey` | `Supabase:ServiceRoleKey` |
| `Supabase__MaintenanceReportBucket` | `Supabase:MaintenanceReportBucket` |

Bucket: `maintenance-reports`. Configure the real values locally through `dotnet user-secrets` for `PRN232.Api`, or through deployment environment variables. Never place the real server key in frontend, tracked config, docs or tests. Docker Compose now passes these variables. Existing Auth configuration was neither copied nor changed.

The live audit used the target configuration and a transaction explicitly set to `READ ONLY`. Verified: both report tables and expected columns exist; reporter/request/image FKs exist; `IX_MaintenanceReports_RequestId` is UNIQUE; bucket `maintenance-reports` has `public = false`. `MaintenanceLogs.OldStatus` is NOT NULL, which conflicts with the required initial log. No corrective SQL was executed.

Target migration files and live migration history contain the same three IDs:

1. `20261003142421_InitialCreate`
2. `20261004073837_RenameKhuTroDichVuToPropertyService`
3. `20261013140609_SeedRolesAndAuthSetup`

There is no migration-ID mismatch. The checked-in model snapshot does not describe the already-existing report tables, and represents OldStatus as required. EF mappings were merged without generating a duplicate table migration or resetting snapshot/history. Future migration reconciliation belongs to the separate schema phase.

At audit time the live requests were Completed: 2, Pending: 1, Approved: 0. The task's expected example state is not the current verified state; no sample row was seeded or rewritten. Live completion UAT was not run.

## TDD and verification

| Stage / behavior | Command or test target | Observed result |
|---|---|---|
| Original baseline | `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --nologo --verbosity quiet` | 36 passed |
| Runtime RED | MaintenanceApiTests anonymous reads | 3 executed failures: list/detail/logs did not enforce 401 |
| Compile RED | Ported Maintenance/report/storage tests before implementation | Missing report DTO/entity/storage/interface implementations; intended missing-feature errors |
| Atomic persistence RED | MaintenanceTransactionTests | 5 passed, 1 failed: Manager assignment not rechecked when completing |
| Atomic persistence GREEN | Same transaction tests after conditional update | 6 passed |
| Swagger RED | Admin_report_missing_is_404_and_swagger_exposes_exact_contracts | Failed: global Bearer requirement also covered public Auth |
| Swagger GREEN | Same test after operation filter registration | Passed in full verified run |
| Approval preservation RED | Existing contract-date and equipment-room test guarantees | 2 executed failures against the ported source behavior |
| Approval preservation GREEN | Same tests after retaining target validation | Passed in full verified run |
| Restore | `dotnet restore PRN232_BE.sln --verbosity quiet` | Exit 0 |
| Docker configuration | `docker compose --env-file .env.example config --quiet` | Exit 0; configuration validation only, no container started |
| Final solution build | `dotnet build PRN232_BE.sln --no-restore --nologo --verbosity quiet` | PASS, 0 warnings / 0 errors in final incremental build |
| Final solution tests | `dotnet test PRN232_BE.sln --no-build --no-restore --nologo --verbosity quiet` | 136 passed, 0 failed, 0 skipped |
| Coverage | `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --collect "XPlat Code Coverage" --settings PRN232.Application.Tests/Maintenance.runsettings` | 136 passed; 566/602 lines = 94.01%, 241/274 branches = 87.95% |

Coverage is scoped to the changed Maintenance service/status classes, controller, current-user adapter, Swagger authorization filter, repository and Storage adapter, including async state machines. It does not claim 80% coverage for unrelated team modules. Per-layer scoped line coverage: Api 87.24%, Application 97.03%, Infrastructure 95.39%.

Earlier fresh compilation reported an existing CS1998 warning in unrelated RoleRepository; that code was not changed. Checkpoint commits were intentionally not made because the user's task explicitly forbids committing. This report and the tests preserve the RED/GREEN evidence without staging or changing Git history.

### Test specification / required guarantee mapping

| Task guarantees | Test target | Type | Result |
|---|---|---|---|
| Customer own creation, Pending, identity, spoof prevention, initial NULL/Pending log (1–8) | MaintenanceFlowApiTests.Target_register_login_and_customer_create_preserve_auth_contract; MaintenanceServiceTests.CreateAsync_* | Unit + HTTP/SQLite | PASS |
| Customer list/detail ownership (9–10) | Lists_use_customer_or_assigned_manager_scope; Filters_cannot_override_ownership; Detail_and_logs_deny_unrelated_users; Customer_cannot_create_for_another_contract | HTTP/SQLite | PASS |
| Admin scope, assignment, automatic Approved and JWT log actor (11–16) | Full_lifecycle_uses_three_jwt_identities_and_private_report_urls; UpdateAsync_approves_and_assigns_atomically_with_one_utc_log | Unit + HTTP/SQLite | PASS |
| Invalid assignees and Admin cannot complete directly (17–21) | Assignment_requires_existing_active_manager; Admin_cannot_complete_by_patch; Role_authorization_uses_target_jwt | HTTP/SQLite | PASS |
| Manager assignment scope and wrong-owner 403 (22–25) | Lists_use_customer_or_assigned_manager_scope; Filters_cannot_override_ownership; Completion_requires_ownership_approved_state_and_existing_request; MaintenanceRepositoryTests | Unit + HTTP/SQLite | PASS |
| Approved-only, repeat/duplicate rejection (26–29) | Completion_requires_ownership_approved_state_and_existing_request; Full_lifecycle_*; CreateReportAsync_rejects_duplicate_report_before_upload | Unit + HTTP/SQLite | PASS |
| Description/count/type/size constraints (30–34) | Report_validates_description_count_type_data_and_size; CreateReportAsync_*; CreateReportAsync_accepts_real_jpeg_and_webp_evidence | Unit + HTTP/SQLite | PASS |
| Reporter spoof prevention, Completed and Manager log actor (35–39) | Full_lifecycle_uses_three_jwt_identities_and_private_report_urls; CreateReportAsync_completes_assigned_request_and_logs_manager_identity | Unit + HTTP/SQLite | PASS |
| Report and images persisted, Admin fields/read URLs/404 (40–49) | Full_lifecycle_*; Admin_report_missing_is_404_and_swagger_exposes_exact_contracts; SupabaseMaintenanceReportImageStorageTests | HTTP/SQLite + mocked HTTP | PASS |
| Anonymous denied (50) | MaintenanceApiTests; Anonymous_writes_and_reports_require_authentication | HTTP | PASS |
| Complete three-event lifecycle, actor order and no new InProgress | Full_lifecycle_uses_three_jwt_identities_and_private_report_urls | Backend end-to-end HTTP/SQLite | PASS |
| Atomic create/assign/complete rollback, unique report and cleanup | MaintenanceTransactionTests; Storage_failure_returns_502_without_completing_or_logging | Relational integration + HTTP | PASS |
| No cleanup of committed evidence on signing failure | Signing_failure_preserves_committed_report_and_returns_502 | HTTP/SQLite | PASS |
| Existing target Auth/JWT/Profile/Admin endpoints retained | Target_auth_*; Existing_target_profile_and_admin_user_endpoints_still_use_same_jwt; MaintenanceCurrentUserTests | HTTP + unit | PASS |

The full backend journey executes real target Auth, JWT middleware, controllers, services and relational repository operations. Only Storage networking is mocked. Live PostgreSQL write concurrency, actual Supabase upload/signing, a deployed server and frontend UAT remain NOT VERIFIED. The live schema/config blockers above must be resolved before those checks can succeed.

## Git and preservation summary

Initial target Git status was clean. Final task change set: 17 modified tracked files, 16 new files, no deletions, no staged changes, no commits/pushes. No teammate endpoint/test file was deleted. All 27 original service test method names remain, with obsolete transition semantics updated to the new specification. Source tracked baseline: 67/67 files unchanged. No tool wrote into P:\.

Changed-file secret-pattern checks were reviewed; the only long-password match is the deliberately synthetic `test-password-123` fixture. Storage test keys are clearly synthetic. No real secret, token or connection string was copied into the change set, outputs or Git. `git diff --check` was also verified.
