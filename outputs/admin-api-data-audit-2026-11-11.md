# Admin API integration and mock-data audit

## Scope and result
- `/management/properties`: assigned Manager names no longer include email addresses. The list renders `primaryImageUrl`, with a non-stock placeholder for missing/broken images.
- `/management/dashboard`: all six metrics, property occupancy rows and unresolved maintenance items now use backend API responses. The shared Manager dashboard uses the same backend-scoped services, not mock assignment IDs.
- `/management/reports`: all four metrics, seven-month revenue chart and expiring contracts now use backend API responses.
- Existing backend endpoints are sufficient; no new endpoint, credentials, migration or database writes were introduced. Earlier Home/search changes were preserved.

## Definitions (not invented statistics)
- Room totals and occupancy use `/api/rooms` and its actual `Available`, `Reserved`, `Occupied`, `Maintenance` statuses.
- Unpaid invoices use `Unpaid` / `Overdue` from `/api/invoices`.
- Revenue is successful invoice payment money (`amountPaid`), grouped by **invoice billing month**, not cash transaction date. `InvoiceReadModelEnricher` supplies the sum of successful invoice payments. Invoice face values, preview invoices and prepaid credit lines are not substituted as cash receipts. Partial receipts count even before full settlement.
- The revenue chart includes the current Vietnamese calendar month and six preceding months, retaining real zero months. It is not a cash-flow-by-payment-date report: the current invoice DTO conversion does not populate payment timestamps.
- Completed maintenance counts all `Completed` requests returned by the API. The UI no longer claims this is a monthly total; the current maintenance list DTO has no completion date.
- Expiring contracts must have status `Active`, a valid start date not in the future, and a valid end date from today through 30 calendar days ahead inclusive. Days remaining are calculated from DateOnly values, using the Vietnamese business date and UTC calendar-day arithmetic. Terminated, expired, invalid and not-yet-started contracts are excluded.
- Loading and API failures do not display sample data or misleading zero statistics. Zero values are shown only for successful empty responses. Refresh/retry fetches new API data; no real-time streaming claim is made.

## Admin page audit
Reviewed management routes in `src/app/router.tsx`, their page imports, shared management components and data hooks. Constants for labels/statuses/form defaults are not treated as mock business data.

| Page | Data source after changes | Frontend mock business data found? |
| --- | --- | --- |
| Dashboard | properties, rooms, invoices, maintenance requests APIs | No |
| Khu trọ | properties and property-manager assignment APIs | No |
| Phòng (global / property-scoped) | rooms, properties, room-types APIs | No |
| Đặt phòng & Đặt cọc | `useBookings` → bookings API | No |
| Lịch xem phòng | `useViewingSchedules` → viewing-schedules API; users/roles API | No |
| Hợp đồng | contracts and billing-timeline APIs | No |
| Hóa đơn / khiếu nại | invoices and invoice-complaints APIs | No |
| Thiết bị | equipment, property and room APIs | No |
| Bảo trì / Sửa chữa | maintenance requests/logs/reports and user/room APIs | No |
| Báo cáo | rooms, invoices, maintenance requests, contracts APIs | No |
| Phân công Manager | users, roles, properties and assignments APIs | No |
| Người dùng | users and roles APIs | No |

`src/data/management.ts`, `utils/managementScope.ts` and `utils/managedInvoices.ts` remain as unused legacy modules, not imports of the audited pages. They were not deleted. Database seed/demo records are a separate concern: records served by the real API remain visible and were not removed or relabelled.

## Image evidence
A read-only check of the public listing returned 30 rentable properties, only one with a primary image (property 2); that image returned HTTP 200. The other 29 need a real image uploaded to populate `primaryImageUrl`. The fix does not fabricate or backfill assets.

## TDD and verification
Journeys came from the user's screenshots and requests, not a separate plan.

| Guarantee | Test / observed evidence |
| --- | --- |
| Property photo and Manager name-only rendering | `tests/managementCrudPages.test.tsx`: new integration reproducer failed before the fix; passes afterward, including failed-image fallback. Existing assignment test updated to assert names and absence of emails. |
| Dashboard figures are authenticated API data, not mock | Initial combined reproducer: 6 failures / 7 passes (five dashboard cases plus image case failed). `npx vitest run tests/managementDashboard.test.tsx tests/managementCrudPages.test.tsx tests/propertyThumbnail.test.tsx` with targeted coverage: 21 passed; 97.87% lines / 93.33% branches across new dashboard/thumbnail code. |
| Report values, chart and expiry window are actual API-derived values | Initial `npx vitest run tests/managementReports.test.tsx --reporter=dot`: 5 intended failures. Final report coverage run: 9 passed; 100% lines / 92.45% branches across report service/page/chart. Covers partial payments, preview/invalid/missing data, zero months, chart heights, 0/30/31-day boundaries, leap year, Vietnam year boundary, 403/server errors, refresh and cancellation. |
| API request contracts | Tests exercise real API client calls with stubbed HTTP responses; assert Bearer authentication, cancellation signals and `cache: no-store`. No real credentials are used. |
| Combined management regressions | `npx vitest run tests/managementReports.test.tsx tests/managementDashboard.test.tsx tests/phase4aManagementApi.test.tsx tests/managementCrudPages.test.tsx tests/propertyThumbnail.test.tsx --reporter=dot`: **36 passed**, 0 failed. |
| Type safety | `npm run typecheck`: passed. |
| Production build | `npm run build`: passed; existing Node deprecation and large-chunk warnings remain. |
| Diff hygiene | `git diff --check`: passed. |

No checkpoint commit, push or worktree was created. Authenticated live Admin figures were not reconciled against the database during this change; automated tests use isolated HTTP fixtures. API reads are separate requests, not one cross-endpoint transactional snapshot. A manual signed-in visual review is still recommended.
