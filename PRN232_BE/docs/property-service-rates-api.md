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

No schema migration or database writes are required. Creating/changing prices is
outside this endpoint; the planned write flow is Admin-only.
