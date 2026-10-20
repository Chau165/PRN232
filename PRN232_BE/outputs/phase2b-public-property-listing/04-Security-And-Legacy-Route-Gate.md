# Phase 2B — Security and legacy-route gate

## New public endpoint

`GET /api/public/properties` is explicitly `[AllowAnonymous]` and returns only `PublicPropertyCardDto` fields: property ID/name/address/ward/district/city, nullable image URL, representative-room amenity summaries, matched rentable-room count, and starting price. It does not serialize a domain entity or reuse `PropertyDto`/`RoomDto`.

The integration test calls the route without a JWT and gets 200. The Swagger test confirms `/api/public/properties` is present with 200/400 responses and no Bearer security requirement. The image field is always null until a public storage policy/host can be verified; opaque and signed image paths are not exposed.

## Existing anonymous API exposure

The following existing GET routes remain unchanged in this task:

| Route | Current source behavior | Exposure |
|---|---|---|
| `GET /api/properties`, `GET /api/properties/{id}` | `PropertiesController.GetAll/GetById` have no `[Authorize]` and return `PropertyDto`, which includes `OwnerId`, `OwnerName`, and total `RoomCount`. The list accepts an `ownerId` query filter. | Reveals owner identity/linkage and total room inventory, including through owner enumeration. |
| `GET /api/rooms`, `GET /api/rooms/{id}` | `RoomsController.GetAll/GetById` have no `[Authorize]`; `RoomDto` includes raw `Status`. Querying does not apply Booking/Contract blockers. | Reveals operational status and can return non-rentable or occupied rooms. |
| `GET /api/rooms/{id}/images`, `GET /api/rooms/{id}/amenities` | Both actions are anonymous and look up rows by RoomId without applying the public rentability policy. | Can reveal images or amenities for rooms that are not publicly rentable. |

Source review found no FE `fetch`, Axios, or `/api/` consumer in `PRN232_FE/src`; Home still renders `src/data/properties.ts` mock data. The backend test project also has no tests targeting the old `/api/properties` or `/api/rooms` paths. This means no in-repository FE dependency was found, but it does not inventory external clients or clients outside this checkout.

## Separate follow-up and release gate

Create a separate legacy-public-GET security/compatibility task before changing these contracts. Inventory deployed/external consumers, define which reads are genuinely public versus manager-only, then migrate callers to allow-listed public DTOs and protect management reads with the established owner/Admin policy. Apply the same publication/rentability check to legacy image and amenity reads if they remain public. Add compatibility/authorization tests before retiring or restricting routes.

**Release gate: Public Browsing is not production-ready while these identified legacy anonymous data-exposure risks remain unresolved.** Creating the new endpoint does not close that gate. Phase 2B changed no legacy route or role rule.
