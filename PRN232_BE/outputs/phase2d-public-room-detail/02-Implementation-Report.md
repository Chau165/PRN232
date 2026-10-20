# Phase 2D - Implementation Report

## Result

**VERIFIED:** Added the anonymous `GET /api/public/rooms/{roomId:int}` API. It returns a current rentable room's allow-listed details and uses Phase 2A availability rules. Existing Phase 2A/B/C behavior and legacy routes were preserved.

## Layer flow

`PublicRoomsController -> IPublicPropertyListingService -> PublicPropertyListingService -> IPublicPropertyListingRepository -> PublicPropertyListingRepository -> AppDbContext`

The existing public browsing service/repository were extended because room detail shares their public DTO and availability boundary. The new route has its own `PublicRoomsController`; it does not overlap `/api/rooms/{id}`. Existing DI registration already covers the extended interfaces, so `Program.cs` required no change.

## Query and consistency plan

1. The service reads `TimeProvider.GetUtcNow()` once, normalizes it to UTC, and passes that instant to Infrastructure.
2. The primary projected query uses `RoomAvailabilityQueries.GetRentableRooms(...)` before selecting the matching room, exact RoomType and allow-listed Property summary.
3. One batch query loads this room's amenities. A second batch query loads services through the selected `RoomTypeId` only.
4. Relational providers run the three reads in one `RepeatableRead` transaction. Non-relational test providers skip transactions as the existing code does.
5. Images and equipment are deliberately not queried. Their response arrays are empty under the documented safety policies.

There is no EF access in Controller/Application service, no domain entity in the response, no N+1 collection load, no status/booking/contract mutation, and no database schema or data change.

## Phase 2A policy reused

**VERIFIED:** `Available` is necessary; `Reserved`, `Occupied`, `Maintenance`, unknown room statuses, unknown booking/contract statuses, a live `Pending` booking, any `Deposited` booking, and any `Active` contract block the detail. `Pending` at exactly its expiry does not block; `Cancelled` does not block by itself. `Active` contracts block regardless of date range.

## Phase 2D source/test files

- New `PRN232.Api/Controllers/PublicRoomsController.cs`.
- Extended `PRN232.Application/DTOs/PublicPropertyListingDto.cs`.
- Extended `IPublicPropertyListingService`, `IPublicPropertyListingRepository`, `PublicPropertyListingService`, and `PublicPropertyListingRepository`.
- Extended API and read-only PostgreSQL tests in `PRN232.Application.Tests`.

No DI, frontend, legacy management endpoint, migration, schema, dependency, booking write path, or production data file was changed.

## Evidence and limits

- **VERIFIED:** Build, all 197 solution tests, focused API/PostgreSQL tests, PostgreSQL read-only session, bounded SQL counts, and focused coverage are recorded in `05-Test-Matrix.md` and `03-PostgreSQL-Verification.md`.
- **VERIFIED:** API tests confirm a single `TimeProvider` read per request, allow-list serialization, generic 404 behavior, exact RoomType pricing/service association, scoped amenities, empty image/equipment arrays, and no room-state mutation.
- **NOT VERIFIED:** Frontend rendering/navigation and browser E2E are not in scope.
- **NOT VERIFIED:** Public image ACL, equipment public status vocabulary, and external consumers of legacy routes are not established.
- **PENDING OWNER APPROVAL:** Anonymous `RoomCode` display and the provisional zero-rentable-property 404 policy.

CodeGraph was attempted for the required navigation and returned `Transport closed`. The listed route, service, repository, policy, entity, mapping, and test paths were traced directly; no full CodeGraph or whole-repository audit is claimed.
