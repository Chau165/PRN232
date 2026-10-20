# Phase 2D - Test Matrix and TDD Evidence

## RED/GREEN

- **RED, VERIFIED:** Before production edits, `Anonymous_room_detail_returns_an_allowlisted_snapshot_without_mutating_room_state` compiled and ran. It failed on the intended missing feature: expected `200 OK`, received `404 Not Found` because the route did not exist. An initial test-draft helper scope compile error was corrected before accepting this valid runtime RED.
- **GREEN, VERIFIED:** The same API test passes after implementation. All 44 public property/room API tests pass.

## Phase 2D guarantees

| Behavior | Test evidence | Result |
|---|---|---|
| Anonymous 200, exact response allow-list, property join, one UTC read, and no mutation | `Anonymous_room_detail_returns_an_allowlisted_snapshot_without_mutating_room_state` | PASS |
| RoomType IDs with equal names keep exact base rent and service association | `Room_detail_uses_exact_room_type_id_for_base_rent_and_services` | PASS |
| Amenities are room-scoped and ordered; absent amenities/services serialize as `[]` | `Room_detail_amenities_are_scoped_and_missing_amenities_or_services_are_empty_arrays`; `Room_detail_uses_the_room_property_and_swagger_exposes_anonymous_route_without_legacy_collision` | PASS |
| Unknown and non-rentable room 404 contracts match; non-integer, zero, and negative route IDs return 404 | `Unknown_and_non_rentable_rooms_return_the_same_generic_not_found_contract` | PASS |
| Reserved, Occupied, Maintenance, unknown room/booking/contract states; future Pending; expired Deposited; future and past Active contracts block | `Unknown_and_non_rentable_rooms_return_the_same_generic_not_found_contract`; `Expired_pending_and_cancelled_bookings_do_not_block_but_active_past_contract_does` | PASS |
| Cancelled booking and Pending exactly at expiry do not block | `Expired_pending_and_cancelled_bookings_do_not_block_but_active_past_contract_does` | PASS |
| Images/equipment and their private fixture metadata do not leak | `Anonymous_room_detail_returns_an_allowlisted_snapshot_without_mutating_room_state` | PASS |
| Swagger documents anonymous route with 200/404; legacy route remains distinct | `Room_detail_uses_the_room_property_and_swagger_exposes_anonymous_route_without_legacy_collision` | PASS |
| PostgreSQL translation, read-only session, eligible/blocked/unknown results, bounded SELECTs | `Room_detail_executes_on_postgresql_read_only_with_bounded_selects` | PASS |

## Commands and results

- `dotnet restore PRN232_BE\PRN232_BE.sln` - passed, all projects up to date.
- `dotnet build PRN232_BE\PRN232_BE.sln --no-restore` - passed, 0 warnings, 0 errors.
- Focused API suite `--filter FullyQualifiedName~PublicPropertyListingApiTests` - 44/44 passed.
- Focused PostgreSQL suite `--filter FullyQualifiedName~PublicPropertyListingPostgreSqlTests` with the configured connection present only in the test process - 3/3 passed; no skipped tests.
- Focused API + PostgreSQL coverage run - 47/47 passed.
- Full solution suite using `work\phase2b-sequential.runsettings` - **197/197 passed, 0 skipped**. Phase 2C baseline was 190 tests; Phase 2D added 6 API tests and 1 PostgreSQL test. No tests were removed or skipped.
- `git diff --check` and a separate whitespace/conflict-marker scan of changed untracked source, tests, and reports - see final verification in the task response/artifacts.

## Coverage

Coverlet collector `XPlat Code Coverage`, .NET 8 test project; focused API and PostgreSQL tests:

| Component | Line coverage | Branch coverage |
|---|---:|---:|
| `PublicRoomsController` | 100% | 100% |
| `PublicPropertyListingService.GetRoomDetailAsync` | 100% | 100% |
| `PublicPropertyListingRepository.GetRoomDetailAsync` | 100% | 100% |
| Entire existing `PublicPropertyListingService` class | 96.77% | 97.05% |
| Entire existing `PublicPropertyListingRepository` class | 100% | 100% |

## Limits

No browser/FE E2E test was run because Phase 2D is backend-only. PostgreSQL evidence is read-only and reflects the verified test instant. Owner approvals and production gates remain listed in `04-Security-Equipment-Images-And-Legacy-Gate.md`.
