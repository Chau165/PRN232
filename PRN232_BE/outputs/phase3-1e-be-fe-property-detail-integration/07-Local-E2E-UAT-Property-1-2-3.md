# Local live-data UAT — Properties 1, 2, and 3

## Runtime used

- Local API: `http://127.0.0.1:55102`, built from the current backend source, with `PublicBrowsing:PropertyDetailEnrichmentEnabled=true` only in that process.
- Local Vite: `http://127.0.0.1:8443`, proxying `/api` to the local API.
- API database connection retained TLS `VerifyFull`; trust-server-certificate was false. All browser traffic to the public API used GET routes.
- Both processes were stopped after UAT. No live deployment or persistent runtime configuration was changed.

## Journey results

| Property | Home card | Detail page | Room list | Room modal |
|---|---|---|---|---|
| 1 | PASS | PASS: description, common Wi-Fi, 4 room amenities | PASS: 1 rentable room | PASS: services listed; no legacy prices |
| 2 | PASS | PASS: description, common Wi-Fi, 5 room amenities with per-room counts | PASS: 2 rentable rooms | PASS: services listed; no legacy prices |
| 3 | PASS | PASS: description, common Wi-Fi, 3 room amenities | PASS: 1 rentable room | PASS: services listed; no legacy prices |

The real API returned `livingCosts: []` for all three because its 12 persisted service rates begin on 2026-11-01, after the current month. Property Detail showed the current-rate update state; Room Detail showed service names and the property-rate update state. No mock values appeared.

A browser screenshot was inspected at a 435×650 viewport for the property detail and room modal. The visible layout remained readable and scrollable. The browser devtools capability was unavailable, so no independent browser-console scan was made. Desktop/tablet visual UAT was not performed.
