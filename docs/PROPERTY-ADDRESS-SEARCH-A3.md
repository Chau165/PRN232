# A3 — Browser address suggestions

The Admin property form sends address searches directly from the browser to Photon when **Tìm thử** is clicked. Typing does not issue searches. The existing BE preview endpoint remains available for compatibility but this form no longer uses it. Create/update APIs and database schema are unchanged.

The form resolves province/ward codes to catalog names before searching. Queries remove a comma between house number and road, optionally remove the prefix “đường” (preserving numbered roads), then try the road without the house number. At most three sequential requests are made, stopping when suitable suggestions are available or the provider fails/rate-limits. A search has a 15-second deadline; successful results are cached for ten minutes in memory, up to 50 searches. Changing address, selecting a pin or unmounting cancels an active search.

Only provider-returned points within the supported coordinate envelope and on the requested road are shown. House numbers are never invented. Suggestions may contain another house number or unverified ward information: the Admin must choose a result and check/adjust the pin. No suggestion is automatically saved. Provider/network failure preserves the current pin and manual map selection remains available.

## Configuration

Optional FE build variable: `VITE_PROPERTY_GEOCODING_URL=https://photon.komoot.io/api/`.
The endpoint must be HTTPS and support browser CORS. This is a public URL, never a secret/API key. Restart Vite or rebuild/redeploy FE after changing it. Browser requests omit credentials and application Authorization headers.

Photon's public demo is best-effort and can throttle requests. For sustained production traffic configure a compatible hosted/self-hosted Photon endpoint with CORS. OSM/Photon attribution appears in the picker. Browser CSP, if configured by hosting, must permit the selected endpoint in connect-src.

## Manual verification

1. Open Admin create/edit property, select province/ward and enter an address.
2. DevTools Network: typing must not trigger Photon requests. Click **Tìm thử**: request host should be Photon, not `/api/properties/location-preview`; no Authorization header.
3. Select a real suggestion, inspect its displayed address, drag the pin and save. Reopen and verify the final dragged coordinates.
4. Change address during lookup; old results must not appear. Simulate offline/429; use a manual pin and save.
5. Missing house numbers are a data limitation; verify the map manually rather than treating a nearby suggestion as an exact match.

Automated coverage: provider query normalization, response validation/deduplication, no app credentials, cache, bounded fallback, throttling, network/JSON errors, abort/deadline, picker stale-result handling, map events and form save/reopen.

Live Photon connectivity could not be verified in the implementation environment (TCP connection to port 443 failed). Verify production browser CORS/connectivity using the steps above.

Provider API reference: https://github.com/komoot/photon/blob/master/docs/api-v1.md
