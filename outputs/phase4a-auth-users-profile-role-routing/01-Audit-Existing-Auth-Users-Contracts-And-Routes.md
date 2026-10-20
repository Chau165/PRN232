# Phase 4A — Existing Auth, Users, Contracts and Routes Audit

Audit scope: source inspection of `C:\PRN232\PRN232_BE` and `C:\PRN232\PRN232_FE` on 2026-10-20. This is source evidence; no live account was used and no database was changed.

## Backend API contract observed

| Endpoint | Contract and authorization from source |
|---|---|
| `POST /api/auth/register` | Body: `fullName`, `email`, `password`, `phoneNumber`. Returns `AuthResponseDto` (`accessToken`, `expiresAtUtc`, `user`). `AuthService` always finds and assigns the `Customer` role; the request DTO has no role field. Duplicate email: 409; validation: 400; missing Customer role: 500. |
| `POST /api/auth/login` | Body: `email`, `password`. Returns the same auth response. Invalid credentials: 401; inactive account: 403. Login has the `LoginPolicy` rate limit. |
| `GET /api/users/me` | Bearer required by `[Authorize]` on `UsersController`; returns `UserDto` with `userId`, `fullName`, `email`, `phoneNumber`, `roleId`, `roleName`, `isActive`; 404 if the subject user is missing. |
| `PUT /api/users/me` | Bearer required. Body: `fullName`, `phoneNumber`; response is `UserDto`. Email, role and status are not accepted for self-edit. |
| `PUT /api/users/me/password` | Bearer required. Body: `currentPassword`, `newPassword`; returns 204. Wrong current password / validation: 400. The service does not state that existing tokens are revoked. |
| `GET /api/users?roleId=` | Admin only. Optional role filter; returns a user list. |
| `GET /api/users/{id}` | Bearer required; Admin may read any user, others only themselves. 403 for another user; 404 if absent. |
| `POST /api/users` | Admin only. Body: `fullName`, `email`, `password`, `phoneNumber`, `roleId`; creates the requested existing role. |
| `PUT /api/users/{id}/role` | Admin only; body `{ roleId }`. Backend prevents changing the caller's own role. |
| `PUT /api/users/{id}/status` | Admin only; body `{ isActive }`. Backend prevents changing the caller's own status. |
| `GET /api/roles` | Public role lookup; use role names to resolve IDs, never hardcode role IDs. |
| `POST /api/roles` | **Current source has no `[Authorize]` and `Program.cs` has no fallback authorization policy.** This permits anonymous role creation and needs an Admin policy. |

JWT source issues `sub`, `email`, name, role and `roleId` claims. `Program.cs` validates issuer, audience, signature and lifetime and disables inbound claim remapping. The FE must use `roleName` from the backend response / `/users/me`; it must not derive authorization from a locally decoded token.

## Frontend source and route inventory

- `LoginPage` uses `authMock` accounts and local matching, displays demo credentials, accepts a phone-or-email label although the API accepts email, and writes a mock session.
- `RegisterPage` checks a hardcoded OTP (`123456`), saves a local mock account and does not call the API.
- `ProfilePage` obtains `getCurrentUser()` with a hardcoded Ngọc Anh fallback; save only shows a toast.
- `Navbar`, `ManagementHeader`, `router.tsx`, and `managementAuth.ts` use localStorage flags/mock role switching (`?as=...`) for authentication and routing.
- `ManagementUsersPage` and `ManagementManagersPage` render `src/data/management` fixtures and toast-only actions. `AssignManagerModal` changes only local selections.
- Existing customer landing route is `/`; existing Manager and Admin landing route is `/management/dashboard`. There is no distinct Admin dashboard route. Profile is `/profile`.
- Customer-only existing routes include `/my-room`, `/my-contracts`, `/my-bills`, `/my-bookings`, `/notifications`, `/deposit`, and `/payment-success`. Management routes use `/management/...`; existing sidebar marks areas, reports, managers and users as Admin-only.
- Public browsing is separate and already calls `/api/public/...` without auth. Its API base normalizes to `/api`; Vite proxies `/api` to `PRN232_DEV_API_PROXY_TARGET`, defaulting to `http://localhost:5102`.

## Management assignment boundary

`Properties.OwnerId` is supplied from the authenticated subject when a property is created. `GET /api/properties?ownerId={id}` can read properties for an owner, but `PropertyDto`/`UpdatePropertyDto` do not provide an owner-transfer field and there is no manager-assignment endpoint. The current assignment modal uses mock property IDs. Phase 4A can show real Manager accounts and their current owned properties and create Manager accounts through Admin-only `POST /api/users`; it cannot persist reassignment without a separate backend contract.

## Authorization matrix from current source

| Role | FE routes / landing | Backend user operations |
|---|---|---|
| Guest | Public Home/browsing, Login, Register. Protected routes redirect to Login. | Auth endpoints public; `GET /api/roles` public. |
| Customer | Home landing; profile and customer portal routes. No management routes. | `/users/me` read/update/password; own `GET /users/{id}` only. |
| Manager | `/management/dashboard`; existing non-admin management routes. Admin-only pages are denied. | `/users/me`; no list/create/role/status access. Property ownership rules remain backend-enforced. |
| Admin | `/management/dashboard`; Admin-only management routes. | User list/create/role/status, subject to self-modification restrictions. |

## Baseline and risks

- The repository had 96 existing modified/untracked status entries before Phase 4A edits. Relevant pre-existing changes include `PRN232_BE/PRN232.Api/Program.cs` and `PRN232_FE/src/constants/routes.ts`; neither is needed for this implementation and both will be preserved.
- BE auth/profile/user services already implement the requested contracts; rewriting them is unnecessary. The direct BE security fix is limited to the public `POST /api/roles` action and a runtime authorization regression test.
- FE token storage will use `sessionStorage` because the BE returns a JWT body and has no HttpOnly-cookie or refresh contract. JavaScript/XSS can still read a session token; no refresh/logout API exists. The UI will expire the session locally and clear it on protected 401. A 403 will not clear the session.
- Existing non-user management business pages still use fixture data and are outside Phase 4A user-management scope. No test identity or live credentials were supplied, so live login/profile smoke testing is not evidenced by this audit.
