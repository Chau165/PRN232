# Phase 2D - PostgreSQL Verification

## Read-only guard

**VERIFIED:** The provider tests opened the configured PostgreSQL connection only inside the test process. The connection helper runs `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY` and asserts `SHOW default_transaction_read_only` returns `on`. No connection string or credential is included here. The test operations below are SELECT-only; the detail's `RepeatableRead` transaction is committed without writing rows.

## Room detail provider test

`PublicPropertyListingPostgreSqlTests.Room_detail_executes_on_postgresql_read_only_with_bounded_selects` passed.

- The eligible candidate was selected through `RoomAvailabilityQueries.GetRentableRooms`; repository detail returned a DTO.
- The actual Npgsql command interceptor captured **3 SELECTs** for eligible detail: projected room/property/RoomType with Phase 2A `NOT EXISTS` availability predicates; `RoomAmenities -> Amenities`; `RoomTypeServices -> Services` for the exact RoomType ID.
- The test verified that SQL does not reference `RoomImages`, `Equipments`, `OwnerId`, `CustomerId`, `Supplier`, or `WarrantyExpiry`.
- A non-Available room returned no detail after **1 SELECT**. An unknown `int.MaxValue` ID returned no detail after **1 SELECT**.
- No raw query parameter values, image values, or credentials are included. Test output reports only query-shape booleans and counts.

The test's candidate-selection queries are separate from the measured detail query counts.

## Current RoomImages profile

**VERIFIED at this test run (2026-10-19 UTC):** The read-only aggregate returned 12 rows, all 12 with HTTPS syntax and an absolute scheme; 1 distinct host; 0 rows classified as `*.supabase.co`, local/private host, Supabase public path, signed path, query string, or signed-query-like. The query reports counts only and does not reveal host names, paths, query strings, or tokens.

These syntax/profile counts do not prove that the host is trusted or that anonymous ACLs permit public access. Therefore the endpoint returns `images: []` and does not query `RoomImages`.

## Existing provider regressions

The Phase 2B listing and Phase 2C property-detail/room-list PostgreSQL tests also passed in the same read-only test run. No schema or data write was performed. Results are bounded to the live database state and test instant; they are not a production readiness approval.
