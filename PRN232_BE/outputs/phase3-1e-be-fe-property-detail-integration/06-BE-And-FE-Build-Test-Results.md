# Backend and frontend build/test results

## Backend

- Focused RED command: `dotnet test PRN232_BE\PRN232.Application.Tests\PRN232.Application.Tests.csproj --filter FullyQualifiedName~PublicPropertyListingServiceTests --no-restore --logger 'console;verbosity=minimal'`. Old behavior failed at UTC 2026-10-31 16:59: expected 2026-10-01, actual 2026-10-31.
- Migrated provider command: same test project filtered to `FullyQualifiedName~Phase31CPropertyDetailPostgreSqlTests`, with the process-only `PRN232_MIGRATION_TEST_POSTGRES_CONNECTION` pointing at the temporary loopback PostgreSQL 17 cluster: **2 passed** (enrichment enabled on current schema and feature-off legacy schema).
- Full regression command: `dotnet test PRN232_BE\PRN232.Application.Tests\PRN232.Application.Tests.csproj --no-restore --logger 'console;verbosity=minimal'` with the same isolated migration-test connection: **201 passed, 4 skipped, 0 failed (205 total)**. The skipped tests require the separate `PRN232_TEST_POSTGRES_CONNECTION` read-only fixture. No skipped test was counted as pass.
- Build succeeded through the test project. Existing `CS1998` warning in `RoleRepository.cs:35` remains unrelated.

## Frontend

- Focused property-page test: `node node_modules/vitest/vitest.mjs run tests/publicPropertyPages.test.tsx --reporter=dot`: **13 passed**.
- Full Vitest run with coverage over the changed client, formatter, pages, and room modal: `node node_modules/vitest/vitest.mjs run --coverage --maxWorkers=1 --reporter=dot --coverage.include=src/api/publicBrowsingApi.ts --coverage.include=src/utils/publicFormatting.ts --coverage.include=src/pages/PropertyDetailPage/PropertyDetailPage.tsx --coverage.include=src/pages/AvailableRoomsPage/AvailableRoomsPage.tsx --coverage.include=src/components/room/RoomDetailModal.tsx`: **31 passed**. Coverage: **90.37% lines, 90% statements, 95.71% functions, 83.13% branches**. The changed Property Detail and modal files have 100% and 93.02% line coverage respectively; configured coverage thresholds passed.
- Existing Node management-contract tests: `node --test tests/managementContracts.test.cjs`: **6 passed**.
- Typecheck: `node node_modules/typescript/bin/tsc --noEmit`: passed.
- Production build: `node node_modules/vite/bin/vite.js build`: passed; 154 modules transformed.
- Vite emitted an existing Node `DEP0205` deprecation warning; build succeeded.

## Tool/runtime notes

`pnpm test`, `pnpm typecheck`, and `pnpm build` aborted before running scripts because pnpm attempted dependency synchronization and refused to remove `node_modules` without a TTY (`ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY`). The already-installed local test/typecheck/build binaries were run directly. No install was run. Node available for the FE run was v26.7.0; Node 22 was not installed.
