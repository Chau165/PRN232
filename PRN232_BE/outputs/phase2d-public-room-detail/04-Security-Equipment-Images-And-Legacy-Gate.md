# Phase 2D - Security, Equipment, Images, and Legacy Gate

## Public DTO allow-list

**VERIFIED:** The new API returns room ID/code/area, RoomType ID/name and exact base rent, a Property summary of ID/name/address/ward/district/city, amenity ID/name pairs, separate service ID/name/price rows, and empty image/equipment arrays. It does not return raw status, owner/customer identifiers, booking/contract/payment/maintenance relations, coordinates, supplier, warranty dates, or admin metadata. API tests assert the serialized field sets and absence of sensitive fixture data.

### Equipment policy

**VERIFIED from source:** `Equipments` contains `Name`, `Status`, `Supplier`, `InstallDate`, and `WarrantyExpiry`. The management controller is role-protected, but `EquipmentService.Validate` accepts any non-empty status string; no controlled public-safe status vocabulary or availability meaning is established. Equipment also has maintenance associations.

**Policy:** `equipments` is always `[]`; the endpoint does not query equipment. Even a fixture row with status `Available` is not advertised. Revisit only after the owner approves public visibility and a safe status rule. No management endpoint was made anonymous.

### Image policy

**VERIFIED at the Phase 2D read-only audit:** 12 values; 12 HTTPS syntax; 1 distinct host; 0 Supabase public/signed path markers, query strings, signed-query-like parameters, or local/private host matches. No host or URL was printed. The host allow-list and anonymous storage ACL are still unverified. `RoomService.AddImageAsync` accepts arbitrary nonblank strings, and the maintenance-report signed-URL workflow is separate and private.

**Policy:** `images` is always `[]`; no stored URL or signed URL is selected. An HTTPS prefix alone is not treated as public authorization.

## Owner and product decisions

- **PENDING OWNER APPROVAL:** `RoomCode` remains in the contract to match Phase 2C room cards. The client must use `RoomId` for navigation and must not parse `RoomCode`.
- **PENDING OWNER APPROVAL:** Phase 2C's conservative 404 behavior for a Property with zero rentable rooms remains unchanged.
- **PENDING OWNER APPROVAL:** Trusted public image host/bucket and anonymous ACL before publishing images.
- **PENDING OWNER APPROVAL:** A public equipment status/visibility vocabulary before returning equipment.

## Separate legacy anonymous GET exposure

**VERIFIED in current controller source and unchanged by Phase 2D:**

| Existing route | Current exposure |
|---|---|
| `GET /api/properties`, `GET /api/properties/{id}` | No controller-level authorization on reads; legacy `PropertyDto` includes owner identity and inventory totals. |
| `GET /api/rooms`, `GET /api/rooms/{id}` | No controller-level authorization on reads; legacy `RoomDto` exposes room status and does not enforce Phase 2A rentability. |
| `GET /api/rooms/{id}/images`, `GET /api/rooms/{id}/amenities` | Reads scoped by room ID without the new public eligibility check. |

**Production security gate: OPEN.** The new allow-listed route does not remediate these separate routes. Recommended follow-up: inventory deployed and external consumers; define public versus owner/Admin reads; migrate public callers to the allow-listed API; add negative authorization and compatibility tests; apply rentability rules to any remaining public image/amenity reads; then restrict/deprecate legacy routes in a separate approved task.

Phase 2D is suitable for Phase 2E development/integration, but **not production release approval** while these route, image, equipment, and owner decisions remain open.
