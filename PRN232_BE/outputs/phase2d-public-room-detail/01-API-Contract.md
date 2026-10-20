# Phase 2D - Public Room Detail API Contract

Status: **VERIFIED** against the implementation and API integration tests.

## Route and behavior

| Item | Contract |
|---|---|
| Method and route | `GET /api/public/rooms/{roomId:int}` |
| Authorization | Anonymous; `[AllowAnonymous]` is explicit. A bearer token is not required. |
| Success | `200 OK` with one `PublicRoomDetailDto`. |
| Unknown or currently non-rentable room | `404 Not Found` with the same generic ProblemDetails `type`, `title`, and `status` (`404`). Per-request `traceId` may differ. No room/property/status explanation is returned. |
| Invalid integer ID | `0` and negative IDs follow normal lookup behavior and return the same 404 envelope. A non-integer value does not match the `int` route constraint and returns framework 404 (route-fallback body is empty). No 400 response is defined. |
| Cache/reservation semantics | No cache header or reservation is added. This GET does not hold a room; future booking writes must recheck availability atomically. |

## Success DTO

Example values below are from the in-memory API fixture and illustrate the JSON shape; they are not a live database record.

```json
{
  "roomId": 102,
  "roomCode": "R102",
  "areaSqm": 20,
  "roomType": {
    "roomTypeId": 102,
    "roomTypeName": "Studio"
  },
  "baseRent": 3000000,
  "property": {
    "propertyId": 1,
    "propertyName": "North_Star 100% Residence",
    "addressText": "Sample Road",
    "ward": "Linh Trung",
    "district": "Thu Duc",
    "city": "TP.HCM"
  },
  "images": [],
  "amenities": [
    { "amenityId": 1, "name": "Wi-Fi" }
  ],
  "services": [
    { "serviceId": 11, "name": "Internet", "price": 100000 }
  ],
  "equipments": []
}
```

## Field mapping and visibility

| JSON field | Source and rule |
|---|---|
| `roomId`, `roomCode`, `areaSqm` | `Rooms`, selected only after the Phase 2A rentability predicate matches the route ID. `RoomCode` remains subject to owner sign-off. |
| `roomType.roomTypeId`, `roomType.roomTypeName` | The selected room's `Rooms.RoomTypeId` and matching `RoomTypes` row. |
| `baseRent` | Exact `Rooms.RoomTypeId -> RoomTypes.Price`; never matched by `TypeName`, never summed with fees. |
| `property` | Related `Properties` row, limited to `PropertyId`, `PropertyName`, `AddressText`, `Ward`, `District`, and `City`. Owner fields and coordinates are omitted. |
| `images` | Always `[]` until a trusted public host and anonymous access policy are verified. `RoomImages` is not queried. |
| `amenities` | `RoomAmenities -> Amenities`, restricted to this `RoomId`, deduplicated and ordered by `AmenityId`. No matches produce `[]`. |
| `services` | `RoomTypeServices -> Services`, restricted to this exact `RoomTypeId`, ordered by `ServiceId`. Each `price` is returned separately. No matches produce `[]`; there is no billing unit or monthly total in the source schema. |
| `equipments` | Always `[]` until safe public status semantics are established. `Equipments` is not queried. |

The response does not contain raw room status, owner/customer IDs, booking, contract, payment, maintenance, supplier, warranty, or tenant data. There is no request body or write side effect.
