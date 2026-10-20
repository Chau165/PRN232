# Phase 2C — Public Property Detail and Room List API

## Routes

Both routes are anonymous and are added to the existing public properties controller. The Phase 2B route GET /api/public/properties remains available with its existing contract.

### GET /api/public/properties/{id}

Returns 200 only when the property currently has at least one rentable room. The allow-listed JSON fields are:

| Field | Type | Meaning |
|---|---|---|
| propertyId | integer | Property identifier |
| propertyName | string | Property name |
| addressText | string | Address |
| ward, district, city | string | Location fields |
| primaryImageUrl | string or null | Always null until a public image policy is verified |
| availableRoomCount | integer | Distinct rentable rooms before any room-list filters |
| startingPrice | decimal or null | Minimum RoomTypes.Price among that same unfiltered rentable-room set |

Example success response:

    {
      "propertyId": 42,
      "propertyName": "Sample Residence",
      "addressText": "Example Road",
      "ward": "Ward 1",
      "district": "District 1",
      "city": "Example City",
      "primaryImageUrl": null,
      "availableRoomCount": 3,
      "startingPrice": 1500000
    }

Unknown and ineligible properties both return 404 with the same generic ProblemDetails type/title/status; a per-request traceId naturally differs. A property with zero rentable rooms also returns 404. This is the prompt-required conservative, provisional privacy choice; the product owner may instead approve a 200 detail response with count zero.

### GET /api/public/properties/{id}/rooms

Returns a page of rentable rooms scoped to the route property ID. Query parameters:

| Parameter | Validation and behavior |
|---|---|
| minPrice | Optional nonnegative decimal; inclusive lower bound |
| maxPrice | Optional nonnegative decimal; inclusive upper bound; must be at least minPrice |
| roomTypeName | Optional trimmed, case-insensitive exact TypeName match across all matching RoomType IDs |
| amenityIds | Repeatable positive integer IDs; duplicates are deduplicated; at most 20 supplied; all IDs must exist and all requested amenities must belong to the same room |
| sortBy | roomId (default), price_asc, or price_desc; price sorts break ties by RoomId ascending |
| page | Optional integer, default 1; at least 1 and offset must fit a 32-bit integer |
| pageSize | Optional integer, default 10; range 1–50 |

The request has no propertyId query parameter. Any such extra query value is ignored; the route ID controls repository scoping. If the property is unknown or currently has no rentable rooms, the endpoint returns the same generic 404. If the property is visible but filters find no rooms, it returns 200 with an empty items array and totalCount zero. An out-of-range page is also 200 with an empty items array and the matching totalCount.

The page fields are items, totalCount, page, and pageSize. totalCount counts distinct rooms after the submitted room filters and before pagination. It is intentionally different from the detail endpoint’s availableRoomCount, which is unfiltered.

Each room item contains only:

| Field | Type |
|---|---|
| roomId | integer |
| roomCode | string |
| roomTypeId | integer |
| roomTypeName | string |
| baseRent | decimal, from the room’s exact RoomTypeId |
| areaSqm | decimal |
| primaryImageUrl | string or null; currently null |
| amenitiesSummary | array of at most five amenityId/name pairs ordered by amenityId |

Example response:

    {
      "items": [
        {
          "roomId": 421,
          "roomCode": "A101",
          "roomTypeId": 7,
          "roomTypeName": "Studio",
          "baseRent": 1500000,
          "areaSqm": 21,
          "primaryImageUrl": null,
          "amenitiesSummary": [{ "amenityId": 2, "name": "Wi-Fi" }]
        }
      ],
      "totalCount": 1,
      "page": 1,
      "pageSize": 10
    }

## Error examples

Invalid query values return 400 ValidationProblemDetails, for example a negative minimum price:

    {
      "errors": {
        "minPrice": ["Minimum price cannot be negative."]
      }
    }

Unknown IDs and IDs with no rentable rooms return 404 without property, owner, room, or status details. The image field remains null; stored RoomImages strings are not copied into public responses.

## Fields intentionally absent

Neither response exposes OwnerId, OwnerName, user/customer identifiers, raw room status, bookings, contracts, payments, or private relations. No property-wide amenities, description, coordinates, distance, occupancy, or equipment metadata is inferred from the frontend mock data.
