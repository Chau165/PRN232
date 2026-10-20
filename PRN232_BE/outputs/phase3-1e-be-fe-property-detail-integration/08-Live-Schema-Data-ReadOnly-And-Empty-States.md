# Live schema/data verification — read only

## Method and boundary

- Used the configured Supabase connection through Npgsql with TLS `VerifyFull`, `TrustServerCertificate=false`, and a database session set to `default_transaction_read_only=on` for catalog/count/rate checks.
- Started the local API against that connection and issued only anonymous public GET requests. `Program.cs` does not run EF migrations or `EnsureCreated` at startup.
- No live DDL, DML, seed, migration, or persistent environment change occurred.

## API GET results

`GET /api/public/properties/{id}` returned HTTP 200 for IDs 1, 2, and 3:

| PropertyId | Description | Common amenities | Room amenities | Current-month living costs |
|---:|---|---|---:|---:|
| 1 | present | 1 (Wi-Fi) | 4 | 0 |
| 2 | present | 1 (Wi-Fi) | 5 | 0 |
| 3 | present | 1 (Wi-Fi) | 3 | 0 |

Home also returned three public cards. Live room pages returned 1, 2, and 1 rentable rooms respectively. The detail response kept the old starting rent and availability fields.

## Live catalog counts

For Properties 1–3: 3 nonblank descriptions, 3 property-amenity links, and 12 property-rate rows (four per property). All 12 rates have `EffectiveFrom=2026-11-01`, `EffectiveTo=NULL`; 0 are effective at the current local month start `2026-10-01`. This explains the correct `livingCosts: []` response in October.

RLS/catalog results for the two new tables are recorded in the security report. No owner/tenant PII or credentials are included here.
