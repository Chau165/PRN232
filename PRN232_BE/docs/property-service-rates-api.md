# Read property service rates

`GET /api/properties/{id}/service-rates`

Requires a bearer token. Admin can read any property; Manager can read only a
property whose `OwnerId` equals the authenticated user ID. Customer is forbidden.

Without `month`, returns all configured historical, current, and future rate
records. Optional `?month=2026-11-01` returns records effective at that month start:
`EffectiveFrom <= month && (EffectiveTo == null || EffectiveTo > month)`.
`EffectiveTo` is exclusive, matching invoice calculations. Month must be a valid
date on the first day of a month. Past and future month filters are allowed.

Response: `200 OK`, ordered by ServiceId ascending, EffectiveFrom descending,
then RateId ascending. All configured property rates are returned, independently
of whether a particular room type uses that service.

```json
[
  {
    "rateId": 1,
    "propertyId": 1,
    "serviceId": 1,
    "serviceName": "Điện",
    "price": 3500,
    "unitCode": "kwh",
    "billingBasis": "usage",
    "effectiveFrom": "2026-11-01",
    "effectiveTo": null
  }
]
```

The example is illustrative; this GET does not seed rates. An existing property
with no matching rates returns `[]`. A missing property returns `404`; a Manager
accessing another owner's property returns `403`, even when it has no rates.
Invalid month returns `400` validation details; missing authentication returns
`401`. Only the month query is accepted as a filter; caller-supplied owner IDs
cannot override authorization.

The GET endpoint requires no schema migration and performs no database writes.

## Create a rate (Admin only)

`POST /api/properties/{id}/service-rates`

Requires an Admin bearer token. Manager is forbidden even for their own property.

```json
{
  "serviceId": 1,
  "price": 3500,
  "unitCode": "kwh",
  "billingBasis": "usage",
  "effectiveFrom": "2026-11-01",
  "effectiveTo": null
}
```

`effectiveTo` may be omitted. Both dates must be month starts, the end is exclusive,
and an end must be later than the start. Price may be zero, must be nonnegative,
and must fit numeric(18,2) without rounding. ServiceId must identify an existing
service. This endpoint does not create the service catalog or room-type links.

Allowed unitCode/billingBasis pairs match the existing database constraints:

| unitCode | billingBasis |
|---|---|
| kwh | usage |
| m3 | usage |
| room_month | fixed_room |
| person_month | occupant |
| vehicle_month | vehicle |

The invoice service still does not bill occupant/vehicle rates; storing those
rates here does not add that calculation support.

Returns `201 Created` with the same object shape as a GET item, including the
generated rateId and serviceName. Location points to the property's rate list.
Invalid fields return `400`; missing property/service returns `404`; overlapping
periods for the same property and service return `409`. Anonymous callers receive
`401`, and non-Admin callers receive `403`.

This is append-only: it never changes an existing price or closes an existing
period. An open-ended existing rate therefore prevents inserting a later rate
for that same property/service. A separate explicit update/version transition
workflow is needed to close that period. Adjacent periods are allowed. Past
periods are allowed; existing invoice snapshots remain unchanged.

PostgreSQL writes lock the property row inside a transaction before checking
overlap and inserting, serializing concurrent POSTs for that property. An existing
database exclusion constraint is also mapped to `409`. No migration is added.
