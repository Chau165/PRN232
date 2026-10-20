# Phase 2C — Security, Legacy Routes, and Image Gate

## New anonymous contracts

The new routes are intentionally anonymous and return only PublicPropertyDetailDto and PublicPropertyRoomCardDto/PageDto fields documented in 01-API-Contracts.md. Tests assert the exact serialized property and room field sets, 404 behavior for hidden/unknown properties, route-ID scoping, and absence of owner/customer IDs, room status, Booking, Contract, and signed-looking fixture URLs. The existing Phase 2B listing is unchanged.

Unknown and ineligible properties use the same generic 404 ProblemDetails type/title/status. ASP.NET emits a distinct traceId for each request; that per-request diagnostic is not a resource-existence signal. Filtered empty results are 200 only after the unfiltered eligibility check succeeds.

Properties have no publication column in the current model. Public visibility therefore follows the approved Phase 2A/2B rule: at least one rentable Room. For a direct detail or room-list request when the count is zero, this implementation returns 404. The prompt identifies this as a provisional conservative security choice; obtain owner approval before changing it to a 200 response with zero availability.

RoomCode is included in the room card as required by the prompt and frontend navigation contract. Product/owner sign-off that RoomCode is safe for anonymous display remains open.

## Existing legacy anonymous exposure — production release blocker

These existing GET routes remain unchanged and are a separate, unresolved production release gate:

| Route | Source behavior | Risk |
|---|---|---|
| GET /api/properties and GET /api/properties/{id} | Unauthenticated controller actions return PropertyDto; the list accepts ownerId. The DTO exposes OwnerId, OwnerName, and total RoomCount. | Owner identity/linkage and total inventory can be enumerated. |
| GET /api/rooms and GET /api/rooms/{id} | Unauthenticated actions return RoomDto with raw Status and do not apply the Phase 2A rentability policy. | Occupied, blocked, or otherwise non-public room state may be exposed. |
| GET /api/rooms/{id}/images and GET /api/rooms/{id}/amenities | Unauthenticated reads by room ID do not apply public rentability/publication eligibility. | Images or amenities for non-rentable rooms may be exposed. |

Hardening plan, to be handled as a separate compatibility task:

1. Inventory deployed and external callers; classify each route as public or management-only and identify its owner.
2. Define and test owner/Admin authorization for management reads, and migrate legitimate public callers to the allow-listed public DTOs.
3. Apply the public visibility/rentability policy to any image or amenity reads that remain public; avoid returning raw legacy DTOs.
4. Deprecate and then retire or restrict legacy anonymous routes only after consumers are migrated and compatibility/authorization tests pass.

Do not treat Phase 2C completion as production release approval while these legacy exposures remain unresolved.

## Image gate

RoomImages.ImageUrl is arbitrary text: the room image write path only rejects blank strings and trims the supplied value. The separate SupabaseMaintenanceReportImageStorage uses privileged credentials and returns signed maintenance-report URLs; it does not establish a public RoomImages host/bucket policy.

The read-only PostgreSQL audit found 12 rows, all with HTTPS syntax, zero relative/opaque values, zero Supabase public/signed path markers, and zero query strings or token-like query parameters. Counts alone do not verify host trust or anonymous ACLs. Both new endpoints therefore return primaryImageUrl as null and do not query or expose stored image strings. Public image integration requires a separate verified allow-list/public-storage policy.

## Remaining non-security limitation

The frontend still uses mock property and room data. No frontend API integration or browser end-to-end validation is included in Phase 2C.
