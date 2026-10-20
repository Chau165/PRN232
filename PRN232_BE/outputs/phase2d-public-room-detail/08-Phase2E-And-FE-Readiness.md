# Phase 2E and Frontend Readiness

## Readiness

**Ready for Phase 2E frontend development/integration against the backend contract. Not ready for production release.**

## FE contract example

Call `GET /api/public/rooms/{roomId}` using the numeric `RoomId` from the Phase 2C room card. Do not parse `RoomCode` for navigation.

```json
{
  "roomId": 421,
  "roomCode": "A101",
  "areaSqm": 21,
  "roomType": { "roomTypeId": 7, "roomTypeName": "Studio" },
  "baseRent": 1500000,
  "property": {
    "propertyId": 42,
    "propertyName": "Sample Residence",
    "addressText": "Example Road",
    "ward": "Ward 1",
    "district": "District 1",
    "city": "Example City"
  },
  "images": [],
  "amenities": [{ "amenityId": 2, "name": "Wi-Fi" }],
  "services": [{ "serviceId": 3, "name": "Internet", "price": 100000 }],
  "equipments": []
}
```

Values above are illustrative. Treat `baseRent` and each service price as distinct amounts; the API does not calculate a monthly total or service billing units. Render an empty state for images/equipment and do not substitute private maintenance-report URLs. A 404 means the room is unknown or is no longer rentable; the API does not disclose which.

## Phase 2E follow-up

1. Replace the Room Detail mock lookup with the anonymous public endpoint and retain `RoomId` for routing.
2. Map the allow-listed property, RoomType, amenities, and separately priced services.
3. Display empty image/equipment states until backend policy approvals are resolved.
4. Add frontend/API contract tests and browser navigation/UAT for Home -> property room list -> room detail, including the 404 case.
5. Keep booking submission outside this read-only endpoint; a future write endpoint must atomically recheck availability and prevent double booking.

## Release gates

- **PENDING OWNER APPROVAL:** anonymous `RoomCode` display and provisional zero-rentable-property 404 behavior.
- **NOT VERIFIED:** trusted public image host plus anonymous ACL; until verified, `images` remains empty.
- **NOT VERIFIED / PENDING OWNER DECISION:** public equipment visibility and a safe status vocabulary; until then, `equipments` remains empty.
- **OPEN production security gate:** inventory consumers and harden the legacy anonymous property/room/image/amenity GET routes, with compatibility and negative authorization tests.
- Frontend rendering and browser E2E remain unverified until Phase 2E is implemented.
