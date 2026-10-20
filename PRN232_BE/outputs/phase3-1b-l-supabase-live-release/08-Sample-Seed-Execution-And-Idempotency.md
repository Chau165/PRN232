# 08 — Sample seed execution and idempotency

**Status: NOT STARTED — no seed SQL or seed data was executed.**

| Check | Result |
|---|---|
| Final owner-approved description/amenity/rate payload | PENDING |
| Live PropertyId / AmenityId / ServiceId lookup | NOT VERIFIED |
| Final idempotent seed payload available | NO |
| Isolated seed rehearsal, run 1 | NOT RUN |
| Isolated seed rehearsal, run 2 | NOT RUN |
| Invalid ServiceId and overlapping-rate negative tests | NOT RUN |
| Live Gate F | NOT STARTED |
| Insert/update/skip/conflict counts | 0 live writes; preview counts remain pending |

SQL_REVIEW/Gate-F-Sample-Seed-REVIEW.sql is deliberately read-only and fails closed when asked to proceed. It is a discovery/review artifact, not a ready seed script. It contains no INSERT or UPDATE. Replace it with a fully mapped, owner-approved, rehearsed transaction only after schema postflight passes.

No invoice, booking, contract, maintenance, user, or utility-reading rows were seeded or changed.