import type { ReactNode } from "react"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementUsersPage from "@/pages/ManagementUsersPage/ManagementUsersPage"
import ManagementManagersPage from "@/pages/ManagementManagersPage/ManagementManagersPage"

const admin = {
  userId: 7,
  fullName: "Admin Thật",
  email: "admin@example.test",
  phoneNumber: "0900000000",
  roleId: 3,
  roleName: "Admin",
  isActive: true,
}
const customer = {
  userId: 8,
  fullName: "Khách Hàng",
  email: "customer@example.test",
  phoneNumber: "0900000001",
  roleId: 1,
  roleName: "Customer",
  isActive: true,
}
const manager = {
  userId: 9,
  fullName: "Manager Thật",
  email: "manager@example.test",
  phoneNumber: "0900000002",
  roleId: 2,
  roleName: "Manager",
  isActive: true,
}
const roles = [
  { roleId: 1, roleName: "Customer" },
  { roleId: 2, roleName: "Manager" },
  { roleId: 3, roleName: "Admin" },
]

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function adminSession() {
  window.sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({
    accessToken: "admin.backend.jwt",
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
  }))
}

function renderPage(page: ReactNode) {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <ProtectedRoute roles={["ADMIN"]}>{page}</ProtectedRoute>
      </AuthProvider>
    </MemoryRouter>,
  )
}

beforeEach(() => window.sessionStorage.clear())

describe("Phase 4A Admin user APIs", () => {
  it("loads users and sends role/status updates with a Bearer token", async () => {
    adminSession()
    const calls: Array<{ url: string; init?: RequestInit }> = []
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/users")) return jsonResponse([admin, customer])
      if (url.endsWith("/roles")) return jsonResponse(roles)
      if (url.endsWith("/users/8/role")) return jsonResponse({ ...customer, roleId: 2, roleName: "Manager" })
      if (url.endsWith("/users/8/status")) return jsonResponse({ ...customer, isActive: false })
      if (url.endsWith("/users/8")) return jsonResponse(customer)
      return jsonResponse({ title: "Not Found" }, 404)
    })
    vi.stubGlobal("fetch", fetchMock)
    renderPage(<ManagementUsersPage />)

    expect(await screen.findByText("customer@example.test")).toBeTruthy()
    const roleSelect = screen.getByLabelText("Đổi role Khách Hàng")
    fireEvent.change(roleSelect, { target: { value: "2" } })
    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/users/8/role"))).toBe(true))
    const roleCall = calls.find((call) => call.url.endsWith("/users/8/role"))!
    expect(roleCall.init?.method).toBe("PUT")
    expect(JSON.parse(String(roleCall.init?.body))).toEqual({ roleId: 2 })
    expect(new Headers(roleCall.init?.headers).get("Authorization")).toBe("Bearer admin.backend.jwt")

    fireEvent.click(screen.getAllByRole("button", { name: "Xem" })[1]!)
    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/users/8"))).toBe(true))
    fireEvent.click(screen.getAllByRole("button", { name: "Khóa" })[1]!)
    await waitFor(() => expect(calls.some((call) => call.url.endsWith("/users/8/status"))).toBe(true))
    const statusCall = calls.find((call) => call.url.endsWith("/users/8/status"))!
    expect(JSON.parse(String(statusCall.init?.body))).toEqual({ isActive: false })
  })

  it("resolves the Manager role from GET /roles and creates an account through POST /users", async () => {
    adminSession()
    const calls: Array<{ url: string; init?: RequestInit }> = []
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/roles")) return jsonResponse(roles)
      if (url.endsWith("/users?roleId=2")) return jsonResponse([manager])
      if (url.endsWith("/properties?ownerId=9")) return jsonResponse([{
        propertyId: 21,
        ownerId: 9,
        ownerName: manager.fullName,
        propertyName: "Khu trọ có thật",
        addressText: "Địa chỉ thật",
        ward: "Phường 1",
        district: "Thủ Đức",
        city: "TP. Hồ Chí Minh",
        roomCount: 4,
      }])
      if (url.endsWith("/users")) return jsonResponse({ ...manager, userId: 10, email: "new.manager@example.test" }, 201)
      return jsonResponse({ title: "Not Found" }, 404)
    })
    vi.stubGlobal("fetch", fetchMock)
    renderPage(<ManagementManagersPage />)

    expect(await screen.findByText("manager@example.test")).toBeTruthy()
    expect(await screen.findByText("Khu trọ có thật")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Phân công" })).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "+ Thêm Manager" }))
    fireEvent.change(screen.getByLabelText("Họ và tên Manager"), { target: { value: "Manager Mới" } })
    fireEvent.change(screen.getByLabelText("Email Manager"), { target: { value: "new.manager@example.test" } })
    fireEvent.change(screen.getByLabelText("Số điện thoại Manager"), { target: { value: "0900000010" } })
    fireEvent.change(screen.getByLabelText("Mật khẩu Manager"), { target: { value: "temp-password" } })
    fireEvent.click(screen.getByRole("button", { name: "Tạo tài khoản" }))

    await waitFor(() => expect(calls.some((call) => call.init?.method === "POST")).toBe(true))
    const createCall = calls.find((call) => call.init?.method === "POST")!
    expect(createCall.url).toMatch(/\/api\/users$/)
    expect(JSON.parse(String(createCall.init?.body))).toEqual({
      fullName: "Manager Mới",
      email: "new.manager@example.test",
      phoneNumber: "0900000010",
      password: "temp-password",
      roleId: 2,
    })
  })
})
