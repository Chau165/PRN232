# Phase 2C — PostgreSQL Verification

## Read-only enforcement

The provider test opens the configured PostgreSQL connection, disables pooling for the test connection, executes SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY, and asserts SHOW default_transaction_read_only returns on before EF queries run. The connection string is supplied to the test process without printing or recording its value. All commands in this verification are SELECTs; no schema, row, setting, or credential was changed.

The focused provider suite passed 2/2. It executed the existing Phase 2B listing verification and the Phase 2C detail/room verification against the configured PostgreSQL database.

## Query and N+1 evidence

The Phase 2C provider test first selects one eligible property through the existing listing query, then performs detail, room-list, unknown-detail, and unknown-room-list queries. The interceptor observes exactly ten EF SELECT commands in total: three for the listing, one for detail, four for a normal room page (visibility, filtered count, page, batched amenities), one for unknown detail, and one for unknown room list.

Captured SQL contains the rentability NOT EXISTS logic, COUNT, MIN, LIMIT/OFFSET, and the bounded RoomAmenities query. The captured SQL contains no RoomImages, OwnerId, or CustomerId projection. The page-batched amenity read demonstrates bounded query count rather than per-room requests. A zero-rentable/unknown room-list result returns null while completing the relational read transaction.

## Sanitized RoomImages profile

The read-only audit queried aggregate counts only and did not select or print any URL, hostname, path, token, or user data:

| Classification | Count |
|---|---:|
| Total RoomImages rows | 12 |
| HTTPS strings | 12 |
| Absolute scheme strings | 12 |
| Relative or opaque strings, derived from total minus absolute schemes | 0 |
| Supabase public-storage path markers | 0 |
| Supabase signed-storage path markers | 0 |
| URLs with a query string | 0 |
| Token/signature/expiry-like query parameters | 0 |

All 12 stored values use HTTPS syntax, but this does not establish that every host is trusted, every object is anonymously readable, or that a future row will follow the same pattern. Therefore the API keeps primaryImageUrl null.

Source review confirms RoomService.AddImageAsync accepts any nonempty string after trimming it. SupabaseMaintenanceReportImageStorage uses a service-role key and signs maintenance-report object paths; it is not the RoomImages storage contract and is not reused for public property/room images.

## Limits

The PostgreSQL fixture is read-only and uses the configured existing database rather than inserting isolated fixtures. It proves the generated EF queries execute on PostgreSQL and checks their query shape, but it does not validate production load, every data permutation, storage ACLs, anonymous image reachability, or frontend rendering. No production mutation or migration was attempted.
