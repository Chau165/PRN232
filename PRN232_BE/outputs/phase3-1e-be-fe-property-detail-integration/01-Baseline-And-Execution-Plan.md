# Phase 3.1E — baseline and execution plan

## Scope

Complete the existing public property-detail path `Supabase PostgreSQL → ASP.NET Core GET → React/Vite` for description, common amenities, rentable-room amenities, and current-month service rates. Preserve existing public contracts and Phase 2A room-availability behavior. No migration, live data write, authentication change, management change, commit, push, or deployment was performed.

## Baseline

- Repository: `C:\PRN232`; BE: `C:\PRN232\PRN232_BE`; FE: `C:\PRN232\PRN232_FE`.
- Starting HEAD: `897b0374bf057767b495b296217fbfb3f72a190e` (`main`).
- Before this phase: 225 Git status rows (37 tracked modified/deleted, 188 untracked); no staged changes. Existing Phase 2/3 work was retained.
- A content snapshot of the 11 affected existing files was made in `%TEMP%\PRN232_Phase3_1E_Baseline_20261020`; the new service test file did not exist at baseline.
- Backend `.codegraph` exists, but the targeted queries returned unrelated symbols; FE has no `.codegraph`. Navigation continued against source and test files directly.

## Plan executed

1. Read repository coding standards, Phase 3.1C-DEV reports, Phase 3.1B-L migration report, and the Phase 3.1E plan.
2. Compare existing API/DTO, repository projection, React API type/client, page, and room modal against the plan.
3. Add a service test for Vietnam current-month-start boundaries; implement the smallest BE fix.
4. Add FE DTO fields and render real API values in the existing page; carry property rates into Room Detail and suppress legacy service prices.
5. Run backend regression/provider tests, FE tests/typecheck/build/coverage, then GET live rows through the local API and complete browser UAT.
6. Record compatibility, live-data, security gates, changed files, and a phase-only patch against the captured baseline.

## Source-of-truth note

The attached plan says the live content tables were empty. That statement is stale: Phase 3.1D's live-write report and this phase's fresh read-only checks show descriptions, Wi-Fi links, and 12 future-effective service rates are already present. This phase did not repeat or amend that seed.
