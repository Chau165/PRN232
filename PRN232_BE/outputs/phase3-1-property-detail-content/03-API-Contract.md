# Phase 3.1 — Public property detail API contract

## Endpoint

`GET /api/public/properties/{id}` (anonymous). Existing detail fields and not-found behavior remain unchanged.

## Added field

```json
"roomAmenities": [
  {
    "amenityId": 1,
    "name": "Wi-Fi",
    "rentableRoomCount": 2
  }
]
```

`roomAmenities` is an array of amenity catalog entries found on at least one currently rentable room of this property. `rentableRoomCount` is the number of this property’s currently rentable rooms carrying that amenity. Entries are scoped to the requested property and use the public room-availability predicate. It is not a property-wide guarantee and does not imply every rentable room has the amenity. An empty array means no such amenities were found on rentable rooms at query time.

The detail response does **not** add `description` or `livingCosts`: neither has a truthful source in the current schema. These sections remain visible in the FE with an unavailable/update state. No fallback/mock values are sent.

## Read-only sample snapshot

Observed on **2026-10-19 (Asia/Saigon)**. This is a direct read-only database calculation for contract/data review, not a captured HTTP response from the running API.

- Property 1, Nhà trọ Bình An: one rentable room, starting rent 3,500,000; Wi-Fi, Air conditioner, Balcony, Private kitchen each count 1.
- Property 2, Khu trọ Sunrise: two rentable rooms, starting rent 2,500,000; Wi-Fi count 2; Air conditioner, Balcony, Private kitchen, Shared washing machine each count 1.
- Property 3, Nhà trọ Minh Tâm: one rentable room, starting rent 3,500,000; Wi-Fi, Air conditioner, Balcony each count 1.

Address and image URLs are omitted from this report. Counts and starting rents can change as availability changes.

## Compatibility and implementation

FE models the added property field as optional during rollout so an older already-running API response renders safely. The DTO itself returns an array. The backend computes the summary in a grouped query in the same repeatable-read detail transaction as the base detail, avoiding an N+1 request/query pattern and keeping the availability snapshot aligned.

The already-running local API on port 5102 returned the previous detail shape and did not include `roomAmenities`; it was not restarted or modified. New live HTTP behavior is therefore **not verified**. Backend serialization and read-only PostgreSQL tests cover the changed contract.
