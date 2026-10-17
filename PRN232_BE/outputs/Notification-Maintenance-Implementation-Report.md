# Maintenance Notifications Implementation Report

## Objective

Add persistent in-app notifications for Maintenance lifecycle events, plus authenticated own-inbox GET/PATCH endpoints. Invoice, Payment, dashboard/report aggregation, and frontend work remain out of scope.

## Initial Repository State

- Worktree was clean on branch `main`; no teammate changes were present.
- `CODING_STANDARDS.md` requires the API -> Application -> Domain / Infrastructure -> Domain layering, DTO responses, thin controllers, repository-owned EF queries, and `Async` suffixes.
- Existing baseline recorded in `docs/database/ef-maintenance-migration-reconciliation.md`: 136 tests passed. Current suite contains 144 tests.
- TDD RED: before implementation, `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --filter FullyQualifiedName~MaintenanceFlowApiTests` ran 57 tests and failed the two new anonymous notification route cases and lifecycle read at HTTP 404. The relevant tests executed; missing routes caused RED.

## Existing Notification Schema And Code

- Entity, `AppDbContext`, initial migration, and `migration2.sql` define `NotificationId`, `UserId`, `Title`, `Type`, and `IsRead` only.
- No existing Notification DTO, interface, service, repository, or controller was present.
- There is no `Message`, `CreatedAt`, reference ID, or reference type column. API responses include only supported columns; list order is `NotificationId DESC`.
- Existing role names are `Customer`, `Manager`, and `Admin`; users expose `IsActive` and role navigation.
- No database documentation identified an alternate Notifications schema.

## Files Created

- `PRN232.Api/Controllers/NotificationsController.cs`
- `PRN232.Application/DTOs/NotificationDto.cs`
- `PRN232.Application/Interfaces/INotificationRepository.cs`
- `PRN232.Application/Interfaces/INotificationService.cs`
- `PRN232.Application/Services/NotificationService.cs`
- `PRN232.Application/Services/NotificationTypes.cs`
- `PRN232.Infrastructure/Repositories/NotificationRepository.cs`
- `outputs/Notification-Maintenance-Implementation-Report.md`
- `outputs/Notification-Maintenance-Diff.patch`
- `outputs/Notification-Maintenance-Changed-Files.txt`

## Files Modified

- `PRN232.Api/Program.cs` - DI registration.
- `PRN232.Application/Interfaces/IMaintenanceRepository.cs` - transaction notification inputs and approving Admin lookup.
- `PRN232.Application/Services/MaintenanceService.cs` - derives recipient IDs and notification titles for successful events.
- `PRN232.Infrastructure/Repositories/MaintenanceRepository.cs` - persists notification rows in existing Maintenance transactions and resolves the assignment log actor.
- `PRN232.Application.Tests/MaintenanceFlowApiTests.cs` - API, ownership, filters, lifecycle, fallback, swagger, and failure tests.
- `PRN232.Application.Tests/MaintenanceServiceTests.cs` - notification collaborator and updated fake repository signatures.
- `PRN232.Application.Tests/MaintenanceTransactionTests.cs` - atomic rollback tests for notification insert failures.

## Files Intentionally Not Modified

- Domain entity and EF mappings/migrations: current schema already contains the required columns.
- Authentication/JWT implementation, Maintenance controllers, Maintenance log behavior, report/image persistence, frontend, and unfinished Invoice/Payment/Dashboard modules.
- No live database, RLS, Supabase Auth, or migration was changed or accessed.

## API And DTOs

- `GET /api/notifications?isRead={true|false}&type={type}` - authenticated; filters are optional.
- `PATCH /api/notifications/{id}` - authenticated; request DTO is `UpdateNotificationDto` with only nullable `IsRead` (required by service validation).
- No `POST /api/notifications` route exists.
- `NotificationDto` contains only `NotificationId`, `Title`, `Type`, and `IsRead`.
- API uses the target JWT through `ICurrentUserService`; reads and writes use JWT `UserId`, not a client-supplied recipient.
- GET returns only the current user’s rows. PATCH returns 404 if absent, 403 for another owner, 400 for missing `isRead`, and only updates `IsRead`.
- All API routes require `[Authorize]`; Swagger exposes Bearer security on GET/PATCH and no POST.

## Architecture And Maintenance Event Recipients

`NotificationsController` -> `INotificationService` -> `INotificationRepository` for inbox access. `MaintenanceService` builds schema-backed notification entities from server-derived recipients; `MaintenanceRepository` owns EF queries and saves event rows within the existing event transaction. Notification type is centralized as `NotificationTypes.Maintenance`.

| Event | Recipients | Title | Persistence |
|---|---|---|---|
| Customer creates request (`NULL -> Pending`) | Every active Admin | `Có yêu cầu bảo trì mới` | Request + initial log + notifications in one repository transaction |
| Admin assigns Manager (`Pending -> Approved`) | Contract Customer and assigned Manager only | Customer: `Yêu cầu bảo trì đã được duyệt`; Manager: `Bạn được giao một yêu cầu bảo trì` | Status + log + notifications in one transaction |
| Assigned Manager completes report (`Approved -> Completed`) | Contract Customer and approving Admin from `MaintenanceLogs.ChangedBy` | Customer: `Yêu cầu bảo trì đã hoàn tất`; Admin: `Yêu cầu bảo trì đã được hoàn tất` | Status + report + images metadata + log + notifications in one transaction |

- Customer creation does not notify the customer; inactive Admins are excluded.
- Assignment recipient IDs derive from the request contract and validated `AssignedTo`; request-body user/role fields are ignored. Existing JWT actor remains `ChangedBy`.
- Completion resolves the authenticating Admin from the `Pending -> Approved` log. If absent, it falls back to all active Admins. The completing Manager is not notified.
- GET operations do not create notifications. Existing lifecycle guards prevent repeated assignment/completion transitions from generating duplicates.

## Transaction And Failure Behavior

- Repository operations use the existing transactions. Added integration tests inject Notification insert failures and verify creation, assignment, and completion state/log/report roll back.
- Completion image upload occurs before DB persistence as before. Upload failure creates no report, completion log, or notification. Failed DB completion rolls back notifications with the report/status/log and uploaded objects continue to be cleaned up by existing behavior.
- Notification retrieval/recipient lookup errors before a business transaction fail the operation; no success alert is silently dropped after a committed Maintenance transition.

## Tests And TDD Evidence

| Guarantee | Test target | Result |
|---|---|---|
| Anonymous GET/PATCH require JWT; notification read is scoped to each authenticated role’s user ID | `MaintenanceFlowApiTests` | PASS |
| Read/type filters work; malformed boolean is rejected; ordering is ID-descending | `Notifications_are_filtered_to_current_user_and_support_current_schema_filters` | PASS |
| PATCH only changes own `IsRead`; other-user update is forbidden; missing row and field use 404/400; extra client fields cannot alter protected data | `User_can_update_only_own_notification_read_state` | PASS |
| Create, assignment, completion recipient matrix, JWT log actors, duplicate completion, no self-alert, and full lifecycle | `Full_lifecycle_uses_three_jwt_identities_and_private_report_urls` | PASS |
| Missing approving log safely falls back to active Admins; excludes inactive Admin and completing Manager | `Completion_without_approval_log_falls_back_to_active_admins_only` | PASS |
| Notification persistence failure rolls back request/log, assignment/log, or report/status/log | `MaintenanceTransactionTests` | PASS |
| Failed image upload produces no completion notification | `Storage_failure_returns_502_without_completing_or_logging` | PASS |
| Swagger exposes protected GET/PATCH only and safe DTO fields | `Admin_report_missing_is_404_and_swagger_exposes_exact_contracts` | PASS |

- RED command: `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --filter FullyQualifiedName~MaintenanceFlowApiTests`; 57 executed, 5 failed because Notification endpoints/behavior were missing (after correcting test compilation errors, 2 anonymous routes and lifecycle reads showed intended 404).
- GREEN command: same focused test target passed 57/57 after implementation.
- Final test command: `dotnet test PRN232_BE.sln --no-restore`; 144 passed, 0 failed, 0 skipped.
- Build command: `dotnet build PRN232_BE.sln --no-restore`; succeeded, 0 warnings, 0 errors.
- Coverage command: `dotnet test PRN232_BE.sln --no-build --collect:"XPlat Code Coverage"`; 144 passed. NotificationService 100% line/branch coverage; NotificationRepository 100% line/branch coverage; NotificationsController 100% line and 50% branch coverage. Overall repository line coverage is 14.09% (1264/8967), below 80% because existing unrelated code is included. This gap is not concealed by changing scope or thresholds.
- Existing Maintenance, auth/JWT, transaction, and image-storage tests remain in place. Suite rose from the recorded 136-test baseline to 144.
- TDD checkpoint commits were not created because the task explicitly prohibits commits.

## Final Review

- Database schema changes by OpenCode: **NONE**.
- `git diff --check`: passed; Git emitted only LF/CRLF normalization warnings.
- Secret review: no credentials, connection strings, service-role keys, or JWT secrets were added. Test fixture credentials remain test-only and unchanged.
- Final `git status --short` and tracked `git diff --stat` are recorded in `Notification-Maintenance-Changed-Files.txt` and summarized below.
- Remaining schema limitation: no message/body or creation timestamp can be returned until database owners add those columns. No schema gap blocks required title/type/read functionality.
- Remaining compatibility note: there is one updated public constructor for `MaintenanceService`; internal test construction sites were updated. No external binary consumers were identified.
- Recommended next task: integrate `NotificationsPage` with `GET /api/notifications` and mark-read `PATCH /api/notifications/{id}` in the frontend repository.

## Final Git Status

The final working-tree status contains 7 modified source/test files, 7 newly created source files, and the three untracked `outputs/` artifacts. Full `git status --short` is in `Notification-Maintenance-Changed-Files.txt`.

`git diff --stat` (tracked files only; Git does not include untracked source/artifact files): 7 files changed, 317 insertions(+), 33 deletions(-). `Notification-Maintenance-Diff.patch` includes tracked changes plus all new `.cs` source files and excludes itself and the report files.
