# Phase 2B — Implementation report

## Result and scope

Implemented the anonymous `GET /api/public/properties` listing endpoint for one card per Property. The endpoint uses a dedicated allow-listed response DTO and does not call the legacy `PropertyDto` path. No frontend, booking/contract write flow, legacy route, database schema/data, or migration was changed.

## Changed implementation files

| File | Change |
|---|---|
| `PRN232.Api/Controllers/PublicPropertiesController.cs` | Adds only `GET /api/public/properties`, explicit `[AllowAnonymous]`, and validation-problem responses. |
| `PRN232.Api/Program.cs` | Registers the new public listing repository and service. Existing `TimeProvider.System` registration is reused. |
| `PRN232.Application/DTOs/PublicPropertyListingDto.cs` | Defines query, normalized search criteria, page, card, and amenity-summary allow-lists. |
| `PRN232.Application/Interfaces/IPublicPropertyListingService.cs` | Adds the listing service contract. |
| `PRN232.Application/Interfaces/IPublicPropertyListingRepository.cs` | Adds the repository contract for page query and amenity-ID validation. |
| `PRN232.Application/Services/PublicPropertyListingService.cs` | Trims and validates filters, pagination, price bounds, sort values, and amenity IDs; reads UTC time once. |
| `PRN232.Infrastructure/Repositories/PublicPropertyListingRepository.cs` | Composes and executes the filtered public query, aggregates, pagination, and page-batched amenities. |
| `PRN232.Application.Tests/PublicPropertyListingApiTests.cs` | Adds 18 API integration cases using the existing WebApplicationFactory pattern and isolated EF InMemory data. |
| `PRN232.Application.Tests/PublicPropertyListingPostgreSqlTests.cs` | Adds opt-in provider-real query verification with a session enforced read-only. |

## Query and data lineage

The service reads one UTC instant from the already-registered `TimeProvider` and passes it to `RoomAvailabilityQueries.GetRentableRooms`. The repository reuses Phase 2A's expression instead of duplicating Booking/Contract rules or loading `GetPropertyAvailabilityAsync()` results into memory.

Room price, logical RoomType name, and repeated amenity filters compose on the same rentable Room query. Amenity requirements are separate correlated `Any` filters, so every requested amenity must be present on one Room. Property name/address/location filters are applied before aggregation. The database returns a distinct Room count and minimum `RoomTypes.Price` from that exact filtered Room set; only Properties with a matching Room are eligible. TypeName matching spans every matching RoomTypeId.

Count and page are separate SQL statements inside one `RepeatableRead` transaction. The transaction gives them a common PostgreSQL snapshot without row locks; the same clock value is reused. A third, page-batched SELECT loads amenities only for each page item's deterministic representative Room (lowest price, then RoomId). PostgreSQL verification observed three SELECT commands per request, not one query per card. No RoomImages or one-to-many image/amenity relation is joined into the aggregate query.

Amenities are ordered by AmenityId and the response caps them at five from the representative Room. `primaryImageUrl` is null while the unconstrained RoomImages URL source has no verified public host/bucket policy. Coordinates, mock distance, description, occupancy, and service fee are omitted.

## Verification

- RED: the initial 15 listing API cases compiled and executed before production changes; all returned 404 because the route did not exist.
- GREEN focused run: with `PRN232_TEST_POSTGRES_CONNECTION` set from the configured connection string, `dotnet test PRN232.Application.Tests\PRN232.Application.Tests.csproj --no-build --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings' --filter FullyQualifiedName~PublicPropertyListing` passed 19/19 (18 API cases and one PostgreSQL verification), zero skipped. The exact settings path is recorded in `05-Test-Matrix.md`.
- `dotnet restore PRN232_BE.sln`: PASS, all projects up to date.
- `dotnet build PRN232_BE.sln --no-restore`: PASS, 0 warnings, 0 errors.
- Full `dotnet test PRN232_BE.sln --no-restore --settings 'C:\Users\Surface\Documents\Codex\2026-10-18\b\work\phase2b-sequential.runsettings'` with the PostgreSQL verification connection set: PASS, 169/169, zero skipped. The final suite includes 150 previously existing tests plus 19 Phase 2B tests; Phase 2A regressions remain in the same suite.
- Focused coverage: `PublicPropertyListingService` 96.49% line / 97.05% branch; public controller and listing repository class entries 100% line / 100% branch. The Cobertura XML used for these coverage figures is under the task `work/` directory, outside the repository; other test-result files are inventoried in `06-Changed-Files.txt`.
- PostgreSQL query execution and SQL-shape evidence are recorded in `03-PostgreSQL-Verification.md`.

The browser/FE flow is not end-to-end verified because FE integration is explicitly outside Phase 2B. The existing frontend still uses local mock data. Existing legacy anonymous route risk remains a separate release gate in `04-Security-And-Legacy-Route-Gate.md`.

No checkpoint commit was created because the Phase 2B prompt explicitly prohibits commits and pushes.
