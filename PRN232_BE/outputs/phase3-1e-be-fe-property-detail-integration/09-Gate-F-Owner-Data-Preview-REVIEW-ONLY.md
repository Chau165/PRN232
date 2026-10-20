# Gate F — live data readiness (review only)

The attached Phase 3.1E plan's “tables still empty” assumption is superseded by the Phase 3.1D live-write report and this phase's read-only recheck. The three properties already have descriptions and one common Wi-Fi amenity each. There is no missing sample-data proposal to approve in this phase.

The following are **existing live rows**, confirmed read-only. Every row has `EffectiveFrom=2026-11-01`, `EffectiveTo=NULL`; they are not displayed by the October current-month API response.

| PropertyId | Service | Price | UnitCode | BillingBasis |
|---:|---|---:|---|---|
| 1 | Electricity - per kWh | 3,500 VND | `kwh` | `usage` |
| 1 | Water - per m3 | 20,000 VND | `m3` | `usage` |
| 1 | Internet - monthly | 100,000 VND | `room_month` | `fixed_room` |
| 1 | Trash - monthly | 30,000 VND | `room_month` | `fixed_room` |
| 2 | Electricity - per kWh | 3,800 VND | `kwh` | `usage` |
| 2 | Water - per m3 | 22,000 VND | `m3` | `usage` |
| 2 | Internet - monthly | 100,000 VND | `room_month` | `fixed_room` |
| 2 | Trash - monthly | 30,000 VND | `room_month` | `fixed_room` |
| 3 | Electricity - per kWh | 3,600 VND | `kwh` | `usage` |
| 3 | Water - per m3 | 20,000 VND | `m3` | `usage` |
| 3 | Internet - monthly | 90,000 VND | `room_month` | `fixed_room` |
| 3 | Trash - monthly | 30,000 VND | `room_month` | `fixed_room` |

These are not proposed demo values and were not written by Phase 3.1E. No SQL seed script is included. No additional migration or live update is recommended from this evidence.
