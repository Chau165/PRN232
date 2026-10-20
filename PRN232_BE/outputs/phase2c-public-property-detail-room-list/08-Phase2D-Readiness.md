# Phase 2D Readiness — Public Room Detail

## Readiness

PHASE 2C COMPLETE — PHASE 2D READY.

The backend contract and provider query path for property detail and rentable room pages are implemented and verified. This is readiness to begin Phase 2D work, not production release approval. The legacy anonymous GET exposure in 04-Security-Legacy-And-Image-Gate.md remains a production release blocker.

## Phase 2D handoff

Implement GET /api/public/rooms/{roomId} as a separate allow-listed public read. Reuse the current Phase 2A rentability query/policy at one request UTC instant. Return the same generic 404 for unknown and currently ineligible rooms; do not reuse the legacy RoomDto or expose raw status, owner/customer identifiers, bookings, or contracts. Keep a room detail query projected in Infrastructure and route/service orchestration in the existing layers.

Before implementation, inspect the current domain mappings and decide exactly which public fields are needed. Load amenities/services/equipment using bounded queries and expose only approved public names/values. Review equipment metadata for operational or tenant information before creating its DTO. Preserve deterministic query count and read consistency; add API and read-only PostgreSQL tests.

## Owner decisions and dependencies

1. Approve anonymous display of RoomCode, which Phase 2C returns in the room list for client navigation.
2. Confirm the provisional 404 policy for a Property with zero rentable rooms, or approve a 200 detail response with zero count.
3. Establish a trusted public image host/bucket and access policy before any RoomImages URL is returned. Until then images remain null; do not reuse maintenance-report signed URLs.
4. Resolve the separate legacy anonymous GET production blocker through consumer inventory, contract/authorization design, migration, and compatibility tests before release.

## Frontend boundary

Phase 2C did not change the frontend or replace its mock data. Phase 2D can provide the room detail contract for the next client integration phase, but browser rendering and end-to-end navigation remain unverified until that frontend work is authorized and implemented.
