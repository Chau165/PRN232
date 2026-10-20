# Phase 2B — Public Property Listing API contract

## Endpoint

`GET /api/public/properties` — anonymous; returns one card per eligible Property. This contract covers the Home listing only. Property detail, room list/detail, FE integration, and all write operations remain out of scope.

## Query parameters

| Parameter | Default | Rules |
|---|---:|---|
| `keyword` | omitted | Trimmed, case-insensitive literal substring across `PropertyName`, `AddressText`, `Ward`, `District`, and `City`. `%`, `_`, and escape characters are treated as literal input, not SQL wildcards. Blank input is ignored. |
| `city`, `district`, `ward` | omitted | Trimmed, case-insensitive exact match against the corresponding real `Properties` column. |
| `minPrice`, `maxPrice` | omitted | Nonnegative decimal bounds, inclusive, applied to the same individual rentable Room. `minPrice` must not exceed `maxPrice`. |
| `roomTypeName` | omitted | Trimmed, case-insensitive exact logical name. It matches every `RoomTypeId` with that name; price remains from each Room's actual `RoomTypeId`. |
| `amenityIds` | omitted | Repeated integer keys, e.g. `?amenityIds=1&amenityIds=3`. IDs must be positive and exist. Duplicate IDs are collapsed; at most 20 values may be submitted. Every requested amenity must belong to the same qualifying Room. |
| `sortBy` | `propertyId` | Accepted values: `propertyId`, `price_asc`, `price_desc` (case-insensitive). Price sorts use `PropertyId` as a stable tie-breaker. |
| `page` | `1` | Integer >= 1. |
| `pageSize` | `10` | Integer in 1..50. |

There is no client-controlled availability clock and no `availableOnly=false`. Radius, distance, occupancy, service-fee, and map-coordinate fields are not returned; the existing UI uses mock map positions and the stored coordinates are too coarse for a meaningful distance claim.

Invalid values return HTTP 400 using the API's validation problem response convention. `totalCount` is the number of matching eligible Properties before pagination.

## Rentable-room and aggregate semantics

For each request the service reads one UTC instant from `TimeProvider`. The query starts from `RoomAvailabilityQueries.GetRentableRooms`, preserving the Phase 2A fail-closed policy:

- Room status must equal `Available` exactly; `Reserved`, `Occupied`, `Maintenance`, and unknown values are excluded.
- `Pending` blocks only while `ExpiryDate > asOfUtc`; `Deposited` always blocks; `Cancelled` does not block by itself; unknown Booking statuses fail closed.
- Active or otherwise unrecognized Contract records block, regardless of dates.
- Read requests do not change persisted statuses.

Room-level price, type, and amenity filters are composed onto the same Room query. A Property is listed only if at least one Room remains after every filter. `availableRoomCount` is the distinct Room count and `startingPrice` is the minimum `RoomTypes.Price` on that exact set. Repeated amenities cannot duplicate a Room or Property. Type names never collapse distinct RoomType IDs or prices.

The representative Room is chosen from the matched set by lowest `RoomTypes.Price`, then lowest `RoomId`. `amenitiesSummary` is drawn only from that Room, ordered by `AmenityId`, capped at five entries, and is not a Property-wide guarantee.

## Response DTO

JSON uses the existing camel-case serializer convention. The DTO is an explicit allow-list and does not expose Owner/tenant IDs, raw Room status, Booking/Contract/payment records, or management entities.

```json
{
  "items": [
    {
      "propertyId": 1,
      "propertyName": "Example Residence",
      "addressText": "Example road",
      "ward": "Example ward",
      "district": "Example district",
      "city": "Example city",
      "primaryImageUrl": null,
      "amenitiesSummary": [
        { "amenityId": 1, "name": "Wi-Fi" }
      ],
      "availableRoomCount": 2,
      "startingPrice": 3000000
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10
}
```

Example values are illustrative, not a claim about live Supabase data. Empty results return HTTP 200 with `items: []` and `totalCount: 0`.

## Image source limitation

The schema has no Property image table. `RoomImages.ImageUrl` is an unconstrained text value; current backend code has no Room image storage provider, bucket policy, signer, or public-host allow-list. The listing will return `primaryImageUrl: null` until public accessibility can be established. It will not publish opaque, signed, or otherwise unverified image paths. A future image-source decision can enable the lowest-`RoomImageId` primary image for the representative Room.

## Query consistency

The repository uses one read-only `RepeatableRead` transaction for eligibility count, page projection, and page-batched representative-room amenities, giving those statements one PostgreSQL snapshot without row locks. The UTC request clock is also shared across every statement. No image query is issued while the image access policy is unresolved.
