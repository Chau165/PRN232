# Frontend components and API mapping

## API client and types

`PublicPropertyDetail` now models optional `description`, `commonAmenities`, and `livingCosts`, preserving compatibility with an older API response. `PublicPropertyServiceRate` models service ID/name, numeric price, unit/billing codes, and effective dates. The existing `getProperty(propertyId)` client call and URL remain unchanged.

## Property Detail

`PropertyDetailPage` renders only API-provided values:

- Description is shown with line breaks preserved. `null`, whitespace-only, or missing values use the existing explicit update state.
- Common amenities render from `commonAmenities`, separate from rentable-room amenities. `[]` or a missing field shows “Chưa có thông tin tiện ích chung.”
- Room amenities continue to use the BE aggregate and display rentable-room counts.
- Living costs render each supported rate in VND with its exact unit label. `[]`/missing fields show “Chi phí sinh hoạt đang được cập nhật.” Unsupported code pairs show a neutral unit-update message without a numeric price. No total is calculated.

## Room Detail

`AvailableRoomsPage` already fetches the property detail and now passes its `livingCosts` to `RoomDetailModal`. The modal matches a room service by `ServiceId` and displays the property rate if supported. If the rate is missing/unsupported it shows an update state; it never displays `PublicRoomServiceDto.Price` from legacy `Services.Price`.

Navigation, room list, filters, pagination, loading/error/404 states, room rent display, and inactive booking actions remain in place.
