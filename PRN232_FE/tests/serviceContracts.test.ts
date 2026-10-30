import { afterEach, describe, expect, it, vi } from "vitest"
import { login, register } from "@/services/authService"
import {
  assignMaintenanceRequest,
  createMaintenanceRequest,
  getMaintenanceLogs,
  getMaintenanceRequest,
  getMaintenanceRequests,
  submitMaintenanceReport,
} from "@/services/maintenanceService"
import {
  changeCurrentPassword,
  createUser,
  getCurrentUser,
  getMaintenanceRooms,
  getPropertiesOwnedBy,
  getRoles,
  getUser,
  getUsers,
  updateCurrentUser,
  updateUserRole,
  updateUserStatus,
} from "@/services/userService"

function response(body: unknown, status = 200): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

afterEach(() => vi.restoreAllMocks())

describe("service API contracts", () => {
  it("keeps authentication endpoints and payload fields unchanged", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(response({ accessToken: "jwt" }))
      .mockResolvedValueOnce(response({ accessToken: "jwt" }))

    await login({ email: "user@example.test", password: "secret" })
    await register({
      fullName: "Người Dùng",
      email: "user@example.test",
      password: "secret",
      phoneNumber: "0900000000",
    })

    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/auth/login")
    expect(String(fetchMock.mock.calls[1][0])).toContain("/api/auth/register")
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({
      email: "user@example.test",
      password: "secret",
    })
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({
      fullName: "Người Dùng",
      email: "user@example.test",
      password: "secret",
      phoneNumber: "0900000000",
    })
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Content-Type")).toBe("application/json")
  })

  it("maps all user operations to their existing methods, paths, tokens and payloads", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({}))
    const controller = new AbortController()

    await getCurrentUser("token")
    await updateCurrentUser("token", { fullName: "An", phoneNumber: "0900" })
    await changeCurrentPassword("token", { currentPassword: "old", newPassword: "new" })
    await getRoles()
    await getUsers("token", 2)
    await getUser("token", 7)
    await createUser("token", {
      fullName: "An",
      email: "an@example.test",
      password: "secret",
      phoneNumber: "0900",
      roleId: 2,
    })
    await updateUserRole("token", 7, 3)
    await updateUserStatus("token", 7, false)
    await getPropertiesOwnedBy("token", 7)
    await getMaintenanceRooms("token", controller.signal)

    const calls = fetchMock.mock.calls
    expect(calls.map(([url]) => new URL(String(url), "http://localhost").pathname)).toEqual([
      "/api/users/me",
      "/api/users/me",
      "/api/users/me/password",
      "/api/roles",
      "/api/users",
      "/api/users/7",
      "/api/users",
      "/api/users/7/role",
      "/api/users/7/status",
      "/api/properties",
      "/api/rooms",
    ])
    expect(new URL(String(calls[4][0]), "http://localhost").searchParams.get("roleId")).toBe("2")
    expect(new URL(String(calls[9][0]), "http://localhost").searchParams.get("ownerId")).toBe("7")
    expect(calls.map(([, init]) => init?.method ?? "GET")).toEqual([
      "GET", "PUT", "PUT", "GET", "GET", "GET", "POST", "PUT", "PUT", "GET", "GET",
    ])
    expect(JSON.parse(String(calls[1][1]?.body))).toEqual({ fullName: "An", phoneNumber: "0900" })
    expect(JSON.parse(String(calls[2][1]?.body))).toEqual({ currentPassword: "old", newPassword: "new" })
    expect(JSON.parse(String(calls[6][1]?.body))).toEqual({
      fullName: "An",
      email: "an@example.test",
      password: "secret",
      phoneNumber: "0900",
      roleId: 2,
    })
    expect(JSON.parse(String(calls[7][1]?.body))).toEqual({ roleId: 3 })
    expect(JSON.parse(String(calls[8][1]?.body))).toEqual({ isActive: false })
    for (const index of [0, 1, 2, 4, 5, 6, 7, 8, 9, 10]) {
      expect(new Headers(calls[index][1]?.headers).get("Authorization")).toBe("Bearer token")
    }
    expect(calls[10][1]?.signal).toBe(controller.signal)
  })

  it("maps maintenance reads, assignment, creation and multipart reports without changing contracts", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({}))
    const controller = new AbortController()
    const form = new FormData()
    form.set("Description", "Fixed")

    await getMaintenanceRequests("token", controller.signal)
    await getMaintenanceRequest("token", 42, controller.signal)
    await getMaintenanceLogs("token", 42, controller.signal)
    await assignMaintenanceRequest("token", 42, { assignedTo: 9, assignmentNote: "Bring part" })
    await createMaintenanceRequest("token", { contractId: 5, equipmentId: null, description: "Broken" }, controller.signal)
    await submitMaintenanceReport("token", 42, form)

    const calls = fetchMock.mock.calls
    expect(calls.map(([url]) => new URL(String(url), "http://localhost").pathname)).toEqual([
      "/api/maintenance-requests",
      "/api/maintenance-requests/42",
      "/api/maintenance-requests/42/logs",
      "/api/maintenance-requests/42",
      "/api/maintenance-requests",
      "/api/maintenance-requests/42/reports",
    ])
    expect(calls.map(([, init]) => init?.method ?? "GET")).toEqual([
      "GET", "GET", "GET", "PATCH", "POST", "POST",
    ])
    expect(JSON.parse(String(calls[3][1]?.body))).toEqual({ assignedTo: 9, assignmentNote: "Bring part" })
    expect(JSON.parse(String(calls[4][1]?.body))).toEqual({ contractId: 5, equipmentId: null, description: "Broken" })
    expect(calls[5][1]?.body).toBe(form)
    expect(new Headers(calls[5][1]?.headers).has("Content-Type")).toBe(false)
    expect(calls[0][1]?.signal).toBe(controller.signal)
    for (const [, init] of calls) {
      expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer token")
    }
  })
})
