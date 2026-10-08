# Maintenance Requests TDD Evidence

Source plan: User-provided Maintenance Request requirements; journeys derived from the requested API behaviors.

## Journeys

- A customer can create a maintenance request for an in-date contract and equipment in the same room.
- A caller can filter, view, and inspect maintenance request history without exposing entities.
- A manager can approve and assign a pending request in one status update, then move it through the allowed status flow.
- Invalid dates, relationships, assignees, and transitions do not create status history.

## Evidence

| Guarantee | Test target | Type | RED evidence | GREEN evidence |
|---|---|---|---|---|
| Create validates contract dates, equipment room, and description; created requests start Pending and unassigned | `MaintenanceServiceTests.CreateAsync_*` | Unit | `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --no-restore` failed to compile because Maintenance service/contracts were not implemented | `dotnet test` passed; included in 36 passing tests |
| List filtering follows `Contract.CustomerId` | `MaintenanceRepositoryTests.GetAllAsync_filters_requests_through_contract_customer_id` | Repository with EF InMemory | Same missing-implementation compile-time RED | `dotnet test` passed; no production database used |
| Detail and logs return null for missing request; logs sort by time then ID | `MaintenanceServiceTests.GetByIdAsync_*`, `GetLogsAsync_*` | Unit | Same missing-implementation compile-time RED | `dotnet test` passed |
| Only Pending->Approved/Rejected, Approved->InProgress, and InProgress->Completed are allowed | `MaintenanceServiceTests.UpdateAsync_*` | Unit | Same missing-implementation compile-time RED | `dotnet test` passed |
| Approval validates active assignee/current contract/equipment and creates one UTC log while preserving omitted assignment | `MaintenanceServiceTests.UpdateAsync_*` | Unit | Same missing-implementation compile-time RED | `dotnet test` passed; repository fake verifies one atomic update call and one log |

## Validation

- `dotnet build`: passed, 0 warnings and 0 errors.
- `dotnet test`: passed, 36 passed, 0 failed, 0 skipped.
- `dotnet test --no-build --collect:"XPlat Code Coverage"`: attempted after the final test addition; test host reported `Out of memory` before producing a report.
- Prior successful coverage run (35 tests): `PRN232.Application` line coverage 86.16%, branch coverage 93.54%. This run included the final service logic; one later regression test covered an additional missing-assignee branch.
- No database rollback integration test was run. The repository uses one EF transaction around conditional request update and log insert; a production-like rollback test needs a relational test provider, which this repository did not previously have.

## Known Gaps

- Authentication, authorization, role taxonomy, and employee role eligibility are intentionally absent.
- Contract status vocabulary is not established by migrations/source, so no Contract status value is enforced.
- `customerId` is a query filter, not authorization.
- No production Supabase connection was used.
