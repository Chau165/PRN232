# Phase 2B — PostgreSQL verification

## Result

**POSTGRESQL EXECUTION VERIFIED — PASS**, using the already-configured remote PostgreSQL connection as the prompt's read-only fallback because the local PostgreSQL service had no usable `postgres` login and no isolated test connection was configured.

No credential or connection value is recorded here. No synthetic rows were inserted. The verification read live listing data only in process and did not print property, owner, tenant, or image values.

## Read-only guard and execution

Test: `PublicPropertyListingPostgreSqlTests.Listing_query_executes_on_postgresql_with_a_read_only_session`.

1. Opens the configured connection without pooling and with bounded connection/command timeouts.
2. Executes `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY` and confirms `SHOW default_transaction_read_only` returns `on` before repository queries.
3. Runs two bounded repository page requests with the production Npgsql provider. The first requests at most two cards; the second exercises keyword, location, price, type-name, amenity, price-sort, and pagination predicates with a unique, non-identifying verification token and multiple filters.
4. The repository uses a `RepeatableRead` transaction for each count/page/amenity batch. The test command interceptor inspected SQL text without parameter values or result data.

The provider-real test passed 1/1. Across the two requests it observed six SELECT commands: one eligible-Property count, one paged aggregate query, and one amenities batch per request. The inspected SQL included `NOT EXISTS`, `COUNT`, `MIN`, `LIMIT`, and `OFFSET`; it did not select `OwnerId` or `CustomerId` and did not query `RoomImages`. No per-card N+1 statements were observed.

The first attempted startup-option read-only guard was not honored by the remote server; that attempt stopped at the guard check before listing data was queried. The successful test switches the current connection session to read-only and verifies the setting before executing repository queries. The session closes after the test.

## Translation and performance notes

- Real PostgreSQL executed the Phase 2A blocker predicates, decimal `MIN`, distinct Room count, price/type/amenity filters, literal-substring keyword query, deterministic price ordering, and pagination.
- Listing count, page, and representative-room amenities are three bounded SELECT statements in one `RepeatableRead` transaction. The amenities statement is page-batched; `pageSize` is limited to 50.
- No `EXPLAIN` or `EXPLAIN ANALYZE` was run against the remote production database. An isolated non-production PostgreSQL dataset was unavailable, so a non-production query plan remains **NOT RUN**.
- The Phase 1 catalog report dated 2026-10-18 recorded Room property/type indexes, Booking/Contract RoomId indexes, and a RoomAmenities key beginning with RoomId. No index, migration, schema, or data change was made in Phase 2B. That prior catalog snapshot was not refreshed as part of this request.

This verification proves provider translation and execution against PostgreSQL, not production load capacity, a non-production query plan, or every live data combination. The in-memory API matrix supplies deterministic business-rule cases.
