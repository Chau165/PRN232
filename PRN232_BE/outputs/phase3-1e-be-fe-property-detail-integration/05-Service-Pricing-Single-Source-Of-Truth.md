# Service pricing — single source of truth

| Purpose | Source | Frontend handling |
|---|---|---|
| Listed room rent | `RoomTypes.Price` through existing property/room DTO fields | Unchanged; not combined with services |
| Current property service rate | `PropertyServiceRates.Price` joined to `Services` by `ServiceId` and filtered by `PropertyId` and current local month | Used by Property Detail and matched Room Detail service |
| Legacy room service amount | `Services.Price` in the existing room-detail response | API contract retained; amount is no longer rendered in the public modal |

Supported exact `(UnitCode, BillingBasis)` labels:

- `(kwh, usage)` → `/kWh`
- `(m3, usage)` → `/m³`
- `(room_month, fixed_room)` → `/phòng/tháng`
- `(person_month, occupant)` → `/người/tháng`
- `(vehicle_month, vehicle)` → `/xe/tháng`

Rates are displayed as individual VND values. The UI does not infer mandatory status, quantities, billing totals, or bundled rent. Unsupported pairs and absent property rates do not fall back to `Services.Price`.

The live rate records are effective from 2026-11-01. At the verified current month start (2026-10-01), the API correctly returned `livingCosts: []`; room modals therefore showed service names with the property-rate update state, not legacy amounts.
