# Phase 2C — Test Matrix and Verification

## RED → GREEN

| Stage | Target | Evidence |
|---|---|---|
| Runtime RED | PublicPropertyListingApiTests after adding detail and room-list cases but before production routes | 38 cases executed: 18 failed because the routes were absent and 20 passed (the existing Phase 2B cases plus checks already satisfied by the generic missing-route response). |
| Compile-time RED | Property_detail_and_room_queries_execute_on_postgresql_read_only_with_bounded_selects | After repairing the test-file line ending/setup, compilation failed only because repository methods GetDetailAsync and GetRoomsAsync did not yet exist. |
| Focused GREEN | PublicPropertyListingApiTests | 38/38 passed, including 20 Phase 2C API cases. |
| Provider GREEN | PublicPropertyListingPostgreSqlTests | 2/2 passed with PRN232_TEST_POSTGRES_CONNECTION set from the configured connection string and a read-only PostgreSQL session. |

## Phase 2C API coverage

| Test method | Cases | Result |
|---|---:|---|
| Anonymous_property_detail_returns_allowlisted_phase2a_aggregates_without_mutating_state | 1 | Pass |
| Unknown_and_ineligible_property_details_return_the_same_not_found_contract | 1 | Pass |
| Anonymous_property_room_list_returns_only_rentable_scoped_rooms_and_allowlisted_fields | 1 | Pass |
| Room_filters_match_one_room_and_logical_type_name_keeps_distinct_type_ids | 1 | Pass |
| Room_sorting_and_pagination_are_stable_with_room_id_tie_break | 1 | Pass |
| Empty_filtered_or_out_of_range_room_page_is_success_for_visible_property | 1 | Pass |
| Invalid_room_list_query_returns_validation_problem | 11 | Pass; invalid page/size/offset, price bounds, malformed price, amenity IDs, and sort values |
| Room_list_uses_route_property_id_and_404s_for_unknown_or_ineligible_properties | 1 | Pass |
| Property_that_loses_all_rentable_rooms_between_home_and_detail_returns_404 | 1 | Pass |
| Swagger_lists_both_public_property_routes_without_bearer_security | 1 | Pass |
| PostgreSQL detail/room query and unknown-ID verification | 1 | Pass; read-only session and bounded SQL shape |

The API tests also verify exact JSON allow-lists, one clock read, no read-side state mutation, same-room amenity filtering, inclusive prices, TypeName matches across IDs, stable RoomId tie-breaks, and image-token non-disclosure.

## Final solution verification

The 169-test pre-Phase-2C suite remained green. The final suite adds 20 API cases and one PostgreSQL provider case.

- dotnet restore PRN232_BE\PRN232_BE.sln — passed; all projects already up to date.
- dotnet build PRN232_BE\PRN232_BE.sln --no-restore — passed; 0 warnings, 0 errors.
- With PRN232_TEST_POSTGRES_CONNECTION set only in the test process from ConnectionStrings__DefaultConnection: dotnet test PRN232_BE\PRN232_BE.sln --no-build --no-restore --settings C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings — passed 190/190, zero skipped.
- Focused public-listing coverage run: 40/40 passed, zero skipped. The Cobertura result is outside the repository at C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2c-coverage-final\e8f9de70-f61d-49bd-b46a-7f441a26d44d\coverage.cobertura.xml.
- Coverage: controller 100% line/branch; service 96.55% line and 97.05% branch; repository 100% line/branch.
- git diff --check and direct untracked-file whitespace review are recorded in 06-Changed-Files.txt after report generation.

No browser/frontend end-to-end test applies because no frontend source changed. PostgreSQL evidence and limits are detailed in 03-PostgreSQL-Verification.md.
