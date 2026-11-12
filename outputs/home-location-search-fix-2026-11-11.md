# Home location search — diagnosis and regression evidence

## User journeys
- A visitor filtering by Quận 7 can find available properties stored under `Khu vực Quận 7 cũ`.
- A visitor searching Quận 7 on the map sees matching property markers, not an unrelated geocoded province.

## Confirmed causes
- Listing filters compared whole stored location strings after lowercasing only.
- Area filters normalized administrative prefixes but not the legacy `Khu vực ... cũ` annotation.
- Typed map searches trusted the first Photon result. A captured public response for `q=Quận 7&countrycode=VN&limit=1` returned a street in Gia Lai.

Read-only checks against the existing local API confirmed properties 9 and 10 have 17 and 18 available rooms and valid coordinates. The running, unchanged Debug server returned zero results for the district filter.

## Changes
- Normalize legacy annotations and existing accent/city aliases consistently for both endpoints; retain exact normalized equality (Quận 7 must not match Quận 70).
- Listing resolves distinct stored location strings, then performs filtering/counting/sorting/pagination in SQL. A location-filtered request costs one additional distinct-location query; no database extension or migration is required.
- Explicit administrative map input is queried locally first, including comma-separated ward/district/city filters. Valid property coordinates center the map without external geocoding. Street/free-text searches retain the existing geocoding path. Missing property coordinates are not fabricated.

## RED → GREEN
Commands below run from the relevant BE or FE directory.

| Guarantee | Command | Observed evidence |
| --- | --- | --- |
| Both APIs find legacy district names and preserve exact district boundaries | `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj --filter FullyQualifiedName~Home_location_search --nologo --verbosity quiet` | Before fix: 8 failed, 2 passed (plain-name controls). After fix: included in passing backend group below. |
| Quận 7 survives an unrelated Photon result | `npx vitest run tests/areaDiscovery.test.ts -t "finds Quận 7"` | Before fix: expected a property, received `[]`. After fix: passing in frontend group below. |
| API/service regressions | `dotnet test PRN232.Application.Tests/PRN232.Application.Tests.csproj -c Release --filter FullyQualifiedName~PublicPropertyListing --nologo --verbosity quiet` | 70 passed, 5 skipped. |
| Map markers, local-first administrative queries, cancellation, errors, unlocated properties, Home regressions | `npx vitest run tests/areaDiscovery.test.ts tests/mapSection.test.tsx tests/publicHome.test.tsx --coverage --coverage.include=src/utils/areaDiscovery.ts --coverage.reportsDirectory=coverage/home-location` | 42 passed; areaDiscovery.ts coverage: 93.28% lines, 90.41% statements, 85.52% branches, 100% functions. Map component test uses a map-rendering stub; not a real-browser Leaflet test. |
| Type safety and production build | `npm run typecheck`; `npm run build` | Both passed; build reports deprecation and chunk-size warnings. |

## Verification limits / applying locally
- Five PostgreSQL-dependent tests were skipped because isolated test connections are not configured. Automated API tests use isolated fixtures; no application database records were changed.
- The running Debug API locks its DLLs. Release tests avoid stopping the user's process. Restart the existing backend and refresh Home to load the fix.
- No worktree, commit, push, or publication was created. Changes remain on the current local branch.
- Regression prevention: keep legacy-address fixtures and the captured incorrect geocoding scenario in the test suite; no architecture rewrite is needed for these fixes.
