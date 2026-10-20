# Phase 2B — Test matrix and TDD evidence

## RED/GREEN and test commands

- Before production code, the initial API tests compiled and ran: **15 failed / 0 passed**, all at the expected `404 Not Found` because `/api/public/properties` did not exist. This was the intended runtime RED.
- `dotnet restore PRN232_BE.sln`: PASS, all projects up to date.
- `dotnet build PRN232_BE.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Focused command: `$env:PRN232_TEST_POSTGRES_CONNECTION=$env:ConnectionStrings__DefaultConnection; dotnet test PRN232.Application.Tests\PRN232.Application.Tests.csproj --no-build --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings' --filter FullyQualifiedName~PublicPropertyListing`. Result: **19 passed / 0 failed / 0 skipped** (18 API cases and one PostgreSQL case). PostgreSQL used the configured connection, enforced session read-only, and performed no inserts or schema changes.
- Full command: `$env:PRN232_TEST_POSTGRES_CONNECTION=$env:ConnectionStrings__DefaultConnection; dotnet test PRN232_BE.sln --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings'`: **169 passed / 0 failed / 0 skipped**. This followed sequential execution because the Phase 2A report documented flaky parallel teardown.
- Focused coverage command: `$env:PRN232_TEST_POSTGRES_CONNECTION=$env:ConnectionStrings__DefaultConnection; dotnet test PRN232.Application.Tests\PRN232.Application.Tests.csproj --no-build --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings' --filter FullyQualifiedName~PublicPropertyListing --collect:'XPlat Code Coverage' --results-directory 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-coverage'`. Result: `PublicPropertyListingService` 96.49% line / 97.05% branch; public controller and listing repository class entries 100% line / 100% branch. The Cobertura XML used for these figures is outside the repository.

## Guarantees

| # | Guarantee | Test evidence | Type | Result |
|---:|---|---|---|---|
| 1 | Anonymous GET returns 200 without a JWT. | `Anonymous_listing_returns_only_allowlisted_property_cards` | API integration | PASS |
| 2 | Results contain one card per eligible Property; multiple Rooms, bookings, amenities, or images do not multiply cards/counts. | `Anonymous_listing_returns_only_allowlisted_property_cards`; `Listing_uses_phase2a_blockers_and_aggregates_only_matching_rentable_rooms` | API integration | PASS |
| 3 | Empty match returns HTTP 200 with `items: []` and `totalCount: 0`. | `No_matching_rentable_room_returns_an_empty_success_page` | API integration | PASS |
| 4 | A Property with no matching rentable Room is excluded. | `Listing_uses_phase2a_blockers_and_aggregates_only_matching_rentable_rooms` | API integration | PASS |
| 5 | Pending blocks before expiry and is non-blocking at equality; Deposited with past expiry remains blocking. | Same blocker test; fixed `TimeProvider` | API integration | PASS |
| 6 | Cancelled Booking does not block by itself. | Same blocker test | API integration | PASS |
| 7 | Future Active Contract blocks; unknown Booking/Contract statuses fail closed. | Same blocker test | API integration | PASS |
| 8 | Reserved, Occupied, Maintenance, and unknown Room statuses are excluded. | Same blocker test; eligible property's count is asserted | API integration | PASS |
| 9 | Count and minimum price use the same matched Room set; a blocked lower-price Room cannot lower the minimum. | Same blocker test; `Room_type_and_amenity_filters_apply_to_one_room_and_keep_same_name_type_ids` | API integration | PASS |
| 10 | Same-name RoomType IDs keep their distinct prices and Properties aggregate independently. | Room-type filter test; pagination/sort test | API integration | PASS |
| 11 | `roomTypeName=Studio` includes all matching RoomType IDs. | `Room_type_and_amenity_filters_apply_to_one_room_and_keep_same_name_type_ids` | API integration | PASS |
| 12 | Price, logical type, and all requested amenities must match the same Room. | Room-type/amenity filter test, including combined filter and duplicate amenity keys | API integration | PASS |
| 13 | Keyword search is case-insensitive/literal for `%` and `_`; city/district/ward use trimmed case-insensitive exact matching. | `Keyword_is_case_insensitive_and_treats_like_wildcards_as_literals` | API integration | PASS |
| 14 | Default ordering, price sorts, stable PropertyId ties, pagination, empty pages, and property-level `totalCount` are deterministic. | `Pagination_and_price_sorts_are_stable_and_total_counts_properties` | API integration | PASS |
| 15 | Invalid page/pageSize/offset, negative or inverted prices, malformed decimals, invalid/unknown amenity IDs, and unsupported sort return 400 validation problems. | `Invalid_listing_parameters_return_a_validation_problem` (10 query cases) | API integration | PASS |
| 16 | Amenity summaries come from the deterministic representative Room and are capped at five; unverified image paths are not exposed. | `Representative_room_amenities_are_bounded_and_unverified_images_are_not_exposed` | API integration | PASS |
| 17 | Public JSON contains only the response allow-list, without owner, status, or Booking fields. | `Anonymous_listing_returns_only_allowlisted_property_cards` | API integration | PASS |
| 18 | Existing management routes/authorization were not changed. | Diff review: Phase 2B changes only new public types/controller/repository, DI registration, tests, and outputs. Existing full suite passes; legacy route exposure remains documented separately. | Source/diff review | PASS |
| 19 | GET does not mutate Room, Booking, or Contract status. | Same blocker test seeds and verifies persisted status after GET | API integration | PASS |
| 20 | Npgsql executes the production listing query with the PostgreSQL provider under a read-only session. | `Listing_query_executes_on_postgresql_with_a_read_only_session`; SQL shape/count assertions | Provider-real PostgreSQL | PASS |
| 21 | One injected UTC instant is used per request. | `Anonymous_listing_returns_only_allowlisted_property_cards` asserts one clock read | API integration | PASS |
| 22 | Filtering, aggregation, pagination, and page amenities stay server-side without per-card queries. | PostgreSQL SQL capture: three SELECT statements per request; `NOT EXISTS`, `COUNT`, `MIN`, `LIMIT`, `OFFSET`; no Owner/Customer IDs or image query | Provider-real PostgreSQL | PASS |

## Known verification limits

- The PostgreSQL run used the configured live remote database with strict session read-only, not a synthetic isolated PostgreSQL dataset. No live values or credentials were copied into output files.
- No non-production `EXPLAIN` plan was available; `EXPLAIN ANALYZE` was not run on the live server.
- The API integration matrix uses EF InMemory for deterministic business-rule and serialization cases; it is not a substitute for the PostgreSQL run.
- FE integration/browser E2E was not run because Phase 2B explicitly excludes FE changes. Home still uses mock data.
- Legacy anonymous route exposure is not resolved by these tests and remains a public-rollout gate.
