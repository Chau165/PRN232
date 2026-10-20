# Phase 4A — Implementation Plan and File Mapping

## Implementation sequence

1. **P0 Audit** — record source contracts, route mapping, authorization matrix, current mocks and gaps in `01-Audit-Existing-Auth-Users-Contracts-And-Routes.md`.
2. **P1 Auth/session** — add a typed API client, Auth/Users API functions and AuthContext. Store only the access token and expiry in tab-scoped session storage; revalidate with `GET /api/users/me` after reload; fail closed for unknown roles; expire/clear on timeout or protected 401; preserve 403 as access denied. Add route guards to existing route objects.
3. **P2 Login/register/profile** — replace mock credentials and OTP with actual API forms; map role names from API responses to existing routes; load and update `/users/me`; add current/new password form; synchronize header and navbar from AuthContext.
4. **P3 User management** — make the Admin user page consume `GET /api/users` and `/api/roles`, details, role and status APIs. Make the Manager page list by the role name returned from `/api/roles`, create Manager accounts through Admin-only `POST /api/users`, and show properties currently owned by each Manager. Do not claim assignment is saved when no write API exists.
5. **P4 Security and verification** — require Admin for `POST /api/roles`, add a TestServer authorization regression, run focused and broad BE/FE validation, and record gaps and a Phase 4A-only patch.

## Planned files

### Backend

- `PRN232_BE/PRN232.Api/Controllers/RolesController.cs` — add Admin-only authorization to role creation; keep public role reads.
- `PRN232_BE/PRN232.Application.Tests/RolesControllerAuthorizationTests.cs` — runtime anonymous/Customer/Admin authorization tests using TestServer and a fake role service.

### Frontend

- `PRN232_FE/src/api/apiClient.ts`, `authApi.ts`, `usersApi.ts` — base URL normalization, typed responses, ProblemDetails errors, and explicit Bearer behavior only for protected requests.
- `PRN232_FE/src/auth/AuthContext.tsx` and `src/components/auth/ProtectedRoute.tsx` — one session source, bootstrap, expiry, logout, role guards and fail-closed routing.
- `src/App.tsx`, `src/app/router.tsx`, `src/types/auth.ts`, `src/types/user.ts` — provider wiring, actual route protection and API/user types.
- `src/pages/LoginPage/LoginPage.tsx`, `RegisterPage/RegisterPage.tsx`, `ProfilePage/ProfilePage.tsx` — real Auth/Profile forms and API error states.
- `src/components/common/Navbar.tsx`, `src/components/management/ManagementHeader.tsx`, `ManagementPage.tsx`, `src/utils/managementAuth.ts` — actual user state and logout; remove mock permission/session decisions.
- `src/pages/ManagementUsersPage/ManagementUsersPage.tsx`, `ManagementManagersPage/ManagementManagersPage.tsx`, `src/components/management/AssignManagerModal.tsx` — API-backed user/manager actions; remove the fake assignment write and state its backend gap.
- `src/components/management/CreateManagerModal.tsx` — Admin-only manager-account creation using role IDs resolved by role name.
- `src/utils/authMock.ts` — remove once all production imports are migrated.
- `PRN232_FE/tests/authRoleRouting.test.tsx` and related test-only additions — API request bodies, role redirects/guards, expiry/401/403, profile/password, management authorization and public browsing isolation.

## Acceptance-to-test mapping

| Guarantee | Planned test evidence |
|---|---|
| Login and register send exact BE DTOs; register cannot submit a role | FE Vitest API integration tests |
| CUSTOMER, MANAGER, ADMIN map only to existing landing routes; unknown role fails closed | FE role routing tests |
| Protected reload calls `/api/users/me` with Bearer; expiry/401 clears session; 403 does not | FE AuthContext/route tests |
| Profile update refreshes current user; password body excludes confirmation | FE profile/API tests |
| Admin-only user list/create/role/status calls carry Bearer; 403 remains an authorization error | FE user-management tests plus existing BE authorization source |
| Anonymous and non-Admin callers cannot create roles; Admin can | BE TestServer integration test |
| Public property browsing stays anonymous | Existing public browsing tests and full FE Vitest run |
| FE production TypeScript and build remain valid | FE typecheck/build |
| No database schema/live data changes | Source diff and migration/status review |

No git checkpoints will be created because the user explicitly prohibited commits. No migration, live seed, deployment, push or third-party write is part of the plan.
