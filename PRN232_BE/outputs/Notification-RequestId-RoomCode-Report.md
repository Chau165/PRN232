# Maintenance Notification Titles: RequestId + RoomCode

## Objective

Make each Maintenance-generated Notification title identify its request and actual room, without changing the API response shape, recipients, lifecycle, authorization, or database schema.

## Initial Repository State

- `git status --short` was not clean before this task. It already contained the uncommitted Maintenance Notification implementation and `outputs/` artifacts from the preceding task. Those changes were preserved; no commit or push was made.
- `CODING_STANDARDS.md` was read and followed: EF projection stays in Infrastructure, application logic uses repository DTOs, and controllers remain untouched.
- TDD RED: `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --filter FullyQualifiedName~Full_lifecycle_uses_three_jwt_identities_and_private_report_urls` failed at the exact create-title assertion. Expected `Có yêu cầu bảo trì mới #25 - Phòng A101`; actual was the old generic `Có yêu cầu bảo trì mới`.

## Files Modified For This Task

- `PRN232.Application/DTOs/MaintenanceRequestDto.cs` - adds room code to the existing contract projection DTO.
- `PRN232.Application/Interfaces/IMaintenanceRepository.cs` - changes request creation notification input to an ID-aware factory.
- `PRN232.Infrastructure/Repositories/MaintenanceRepository.cs` - projects `Contracts.Room.RoomCode`; obtains the generated RequestId and saves notifications before committing the existing transaction.
- `PRN232.Application/Services/MaintenanceService.cs` - formats all five requested titles from trusted request/contract values.
- `PRN232.Application.Tests/MaintenanceFlowApiTests.cs` - makes the end-to-end fixture deterministic (`RequestId = 25`, `RoomCode = A101`), verifies exact titles, different-room isolation, recipient rules, GET title, and unchanged PATCH behavior.
- `PRN232.Application.Tests/MaintenanceServiceTests.cs` - asserts the generated ID and RoomCode in the create notification title.
- `PRN232.Application.Tests/MaintenanceTransactionTests.cs` - adapts the creation-failure fixture to the ID-aware factory; keeps transaction rollback coverage.

The current uncommitted worktree also contains earlier Notification implementation files and changes. They are unrelated to this title-format task and were not reverted or otherwise modified here. The generated patch is the complete current source diff relative to Git `HEAD`, so it includes those existing uncommitted Notification changes as well as this task's changes.

## Trusted Data Sources

- RequestId for creation is read from `MaintenanceRequests.RequestId` after EF inserts the request inside the existing repository transaction. The repository then invokes the notification title factory with that generated ID.
- RequestId for approval/completion is the authenticated route's existing request ID, after the repository confirms/provides the associated request data.
- RoomCode for create/approval comes from the validated `MaintenanceContractDto`, now projected by the repository from `Contracts.Room.RoomCode`.
- RoomCode for completion comes from the existing `MaintenanceRequestDto` projection through `MaintenanceRequests.Contract.Room.RoomCode`.
- No client-supplied RequestId, RoomCode, or recipient value is used.

## Exact Titles

| Event | Exact title format |
|---|---|
| Customer creates request | `Có yêu cầu bảo trì mới #{RequestId} - Phòng {RoomCode}` |
| Customer receives approval | `Yêu cầu bảo trì #{RequestId} - Phòng {RoomCode} đã được duyệt` |
| Assigned Manager receives assignment | `Bạn được giao yêu cầu bảo trì #{RequestId} - Phòng {RoomCode}` |
| Customer receives completion | `Yêu cầu bảo trì #{RequestId} - Phòng {RoomCode} đã hoàn tất` |
| Admin receives completion | `Yêu cầu bảo trì #{RequestId} - Phòng {RoomCode} đã được hoàn tất` |

The full lifecycle test asserts all five strings exactly using request `25`, room `A101`, while another maintenance request is associated with a different room (`A202`). It also checks recipient IDs and existing `Pending -> Approved -> Completed` logs.

## Repository, Service, And Transaction Behavior

- `MaintenanceContractDto` now carries `RoomCode`; `MaintenanceRepository.GetContractAsync` projects it from `c.Room!.RoomCode`.
- `MaintenanceService` uses the already validated contract RoomCode for create/approval and the existing request projection for completion.
- For creation, generated RequestId is not available before insertion. `AddWithInitialLogAsync` now accepts `Func<int, IReadOnlyCollection<Notifications>>`, saves the request and initial log to obtain the ID, saves notifications generated with that ID, then commits the same transaction. Notification insert failures still roll the request and log back.
- Assignment/completion Notification persistence remains inside their existing repository transactions. No notification was moved outside a transaction.
- Notification recipients and missing-approver fallback are unchanged.

## Tests

- Updated full lifecycle assertions for the five exact titles, `RequestId = 25`, `RoomCode = A101`, and distinct-room isolation (`A202`). Existing recipient assertions remain.
- Updated API verification still confirms `GET /api/notifications` returns the stored title and PATCH modifies only the read state.
- Added unit assertion that generated request ID and contract RoomCode produce the correct create title.
- Existing Notification insert failure rollback tests remain and use the new request-ID factory signature.
- RED command above failed on the old create title, as intended.
- Focused GREEN command: `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~Full_lifecycle_uses_three_jwt_identities_and_private_report_urls|FullyQualifiedName~CreateAsync_creates_pending_request_for_valid_contract_and_equipment|FullyQualifiedName~MaintenanceTransactionTests"`; **11 passed, 0 failed**.

## Verification

- `dotnet restore`: passed; all projects up-to-date.
- `dotnet build`: passed, 0 warnings, 0 errors.
- `dotnet test`: passed, **144 passed, 0 failed, 0 skipped**. Baseline was 144 tests; existing test cases were strengthened, not removed.
- UTF-8 review: source contains readable Vietnamese strings; no searched mojibake patterns (`C├`, `Y├`, `bß`) were found.
- `git diff --check`: passed. Git emitted only its existing LF/CRLF normalization warnings.
- No secret, connection string, access token, JWT secret, or Supabase key was added.

## Scope Confirmations And Limitations

- Database schema changed: **NO**. No migration or database operation was performed.
- Notification recipients changed: **NO**.
- Auth/JWT changed: **NO**.
- Notification API contract changed: **NO**.
- Frontend changed: **NO**.
- Maintenance lifecycle/transactions changed: **NO**; notification writes remain atomic with their corresponding Maintenance persistence.
- Limitation/blocker: none. Current schema supports the requested title strings.

## Git Diff Statistics

At review time, tracked diff stat (includes the pre-existing uncommitted Notification implementation described above):

```text
 PRN232_BE/PRN232.Api/Program.cs                    |   2 +
 .../MaintenanceFlowApiTests.cs                     | 168 +++++++++++++++++++--
 .../MaintenanceServiceTests.cs                     |  54 +++++--
 .../MaintenanceTransactionTests.cs                 |  68 ++++++++-
 .../DTOs/MaintenanceRequestDto.cs                  |   1 +
 .../Interfaces/IMaintenanceRepository.cs           |  16 +-
 .../Services/MaintenanceService.cs                 |  29 +++-
 .../Repositories/MaintenanceRepository.cs          |  37 ++++-
 8 files changed, 338 insertions(+), 37 deletions(-)
```

`outputs/Notification-RequestId-RoomCode-Diff.patch` contains the final Git source diff and excludes this report and itself.

Final `git status --short`:

```text
M PRN232.Api/Program.cs
M PRN232.Application.Tests/MaintenanceFlowApiTests.cs
M PRN232.Application.Tests/MaintenanceServiceTests.cs
M PRN232.Application.Tests/MaintenanceTransactionTests.cs
M PRN232.Application/DTOs/MaintenanceRequestDto.cs
M PRN232.Application/Interfaces/IMaintenanceRepository.cs
M PRN232.Application/Services/MaintenanceService.cs
M PRN232.Infrastructure/Repositories/MaintenanceRepository.cs
?? PRN232.Api/Controllers/NotificationsController.cs
?? PRN232.Application/DTOs/NotificationDto.cs
?? PRN232.Application/Interfaces/INotificationRepository.cs
?? PRN232.Application/Interfaces/INotificationService.cs
?? PRN232.Application/Services/NotificationService.cs
?? PRN232.Application/Services/NotificationTypes.cs
?? PRN232.Infrastructure/Repositories/NotificationRepository.cs
?? outputs/
```
