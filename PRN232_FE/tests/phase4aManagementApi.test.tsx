import type { ReactNode } from "react"
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementUsersPage from "@/pages/ManagementUsersPage/ManagementUsersPage"
import ManagementManagersPage from "@/pages/ManagementManagersPage/ManagementManagersPage"
import ManagementPropertiesPage from "@/pages/ManagementPropertiesPage/ManagementPropertiesPage"

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

function managerSession() {
  window.sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({
    accessToken: "manager.backend.jwt",
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

function renderManagerPage(page: ReactNode) {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <ProtectedRoute roles={["ADMIN", "MANAGER"]}>{page}</ProtectedRoute>
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
    let managerHasProperty = false
    const property = {
      propertyId: 21,
      ownerId: 7,
      ownerName: admin.fullName,
      propertyName: "Khu trọ có thật",
      addressText: "Địa chỉ thật",
      ward: "Phường 1",
      district: "Thủ Đức",
      city: "TP. Hồ Chí Minh",
      roomCount: 4,
    }
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/roles")) return jsonResponse(roles)
      if (url.endsWith("/users?roleId=2")) return jsonResponse([manager])
      if (url.endsWith("/managers/9/properties")) return jsonResponse(managerHasProperty ? [property] : [])
      if (url.endsWith("/properties")) return jsonResponse([property])
      if (url.endsWith("/properties/21/managers") && init?.method === "POST") {
        managerHasProperty = true
        return jsonResponse({
          propertyId: 21,
          propertyName: property.propertyName,
          managerId: 9,
          managerName: manager.fullName,
          managerEmail: manager.email,
          managerIsActive: true,
        }, 201)
      }
      if (url.endsWith("/users")) return jsonResponse({ ...manager, userId: 10, email: "new.manager@example.test" }, 201)
      return jsonResponse({ title: "Not Found" }, 404)
    })
    vi.stubGlobal("fetch", fetchMock)
    renderPage(<ManagementManagersPage />)

    expect(await screen.findByText("manager@example.test")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Phân công khu trọ" }))
    const assignmentDialog = await screen.findByRole("dialog", { name: "Phân công khu trọ" })
    fireEvent.click(within(assignmentDialog).getByRole("checkbox", { name: /Khu trọ có thật/ }))
    fireEvent.click(within(assignmentDialog).getByRole("button", { name: "Lưu phân công" }))
    await waitFor(() => expect(calls.some((call) =>
      call.url.endsWith("/properties/21/managers") && call.init?.method === "POST",
    )).toBe(true))
    const assignmentCall = calls.find((call) => call.url.endsWith("/properties/21/managers") && call.init?.method === "POST")!
    expect(JSON.parse(String(assignmentCall.init?.body))).toEqual({ managerId: 9 })
    expect(await screen.findByRole("button", { name: "Thu hồi Manager Thật tại Khu trọ có thật" })).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "+ Thêm Manager" }))
    fireEvent.change(screen.getByLabelText("Họ và tên Manager"), { target: { value: "Manager Mới" } })
    fireEvent.change(screen.getByLabelText("Email Manager"), { target: { value: "new.manager@example.test" } })
    fireEvent.change(screen.getByLabelText("Số điện thoại Manager"), { target: { value: "0900000010" } })
    fireEvent.change(screen.getByLabelText("Mật khẩu Manager"), { target: { value: "temp-password-1" } })
    fireEvent.click(screen.getByRole("button", { name: "Tạo tài khoản" }))

    await waitFor(() => expect(calls.some((call) => call.init?.method === "POST")).toBe(true))
    const createCall = calls.find((call) => call.url.endsWith("/users") && call.init?.method === "POST")!
    expect(createCall.url).toMatch(/\/api\/users$/)
    expect(JSON.parse(String(createCall.init?.body))).toEqual({
      fullName: "Manager Mới",
      email: "new.manager@example.test",
      phoneNumber: "0900000010",
      password: "temp-password-1",
      roleId: 2,
    })
  })

  it("prechecks existing assignments and explains a legacy OwnerId pointing to a Manager", async () => {
    adminSession()
    const legacyProperty = {
      propertyId: 23,
      ownerId: 9,
      ownerName: manager.fullName,
      propertyName: "Khu trọ OwnerId cũ",
      addressText: "Địa chỉ cũ",
      ward: "",
      district: "",
      city: "",
      roomCount: 2,
    }
    const calls: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/roles")) return jsonResponse(roles)
      if (url.endsWith("/users?roleId=2")) return jsonResponse([manager])
      if (url.endsWith("/properties")) return jsonResponse([legacyProperty])
      if (url.endsWith("/managers/9/properties")) return jsonResponse([legacyProperty])
      return jsonResponse({ title: "Not Found" }, 404)
    }))

    renderPage(<ManagementManagersPage />)
    fireEvent.click(await screen.findByRole("button", { name: "Phân công khu trọ" }))
    const dialog = await screen.findByRole("dialog", { name: "Phân công khu trọ" })
    const legacyCheckbox = within(dialog).getByRole("checkbox", {
      name: /Khu trọ OwnerId cũ/,
    }) as HTMLInputElement

    expect(legacyCheckbox.checked).toBe(true)
    expect(legacyCheckbox.disabled).toBe(true)
    expect(within(dialog).getByText(/OwnerId=9.*Manager/)).toBeTruthy()
    expect(within(dialog).getByRole("button", { name: "Lưu phân công" }).hasAttribute("disabled")).toBe(true)
    expect(calls.some((call) => call.init?.method === "POST")).toBe(false)
  })

  it("refreshes persisted assignments and reports a partial save with the failed property", async () => {
    adminSession()
    const propertyOne = {
      propertyId: 21,
      ownerId: 7,
      ownerName: admin.fullName,
      propertyName: "Khu trọ Một",
      addressText: "Địa chỉ Một",
      ward: "",
      district: "",
      city: "",
      roomCount: 2,
    }
    const propertyTwo = {
      ...propertyOne,
      propertyId: 22,
      propertyName: "Khu trọ Hai",
    }
    const calls: Array<{ url: string; init?: RequestInit }> = []
    let propertyTwoAssigned = false
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.endsWith("/roles")) return jsonResponse(roles)
      if (url.endsWith("/users?roleId=2")) return jsonResponse([manager])
      if (url.endsWith("/managers/9/properties")) {
        return jsonResponse(propertyTwoAssigned ? [propertyOne, propertyTwo] : [propertyOne])
      }
      if (url.endsWith("/properties")) return jsonResponse([propertyOne, propertyTwo])
      if (url.endsWith("/properties/21/managers/9") && init?.method === "DELETE") {
        return jsonResponse({
          title: "Assignment blocked",
          detail: "Khu trọ Một còn công việc đang mở.",
        }, 409)
      }
      if (url.endsWith("/properties/22/managers") && init?.method === "POST") {
        propertyTwoAssigned = true
        return jsonResponse({
          propertyId: 22,
          propertyName: propertyTwo.propertyName,
          managerId: 9,
          managerName: manager.fullName,
          managerEmail: manager.email,
          managerIsActive: true,
        }, 201)
      }
      return jsonResponse({ title: "Not Found" }, 404)
    })
    vi.stubGlobal("fetch", fetchMock)

    renderPage(<ManagementManagersPage />)
    fireEvent.click(await screen.findByRole("button", { name: "Phân công khu trọ" }))
    const dialog = await screen.findByRole("dialog", { name: "Phân công khu trọ" })
    fireEvent.click(within(dialog).getByRole("checkbox", { name: /Khu trọ Một/ }))
    fireEvent.click(within(dialog).getByRole("checkbox", { name: /Khu trọ Hai/ }))
    fireEvent.click(within(dialog).getByRole("button", { name: "Lưu phân công" }))

    const alert = await within(dialog).findByRole("alert")
    expect(alert.textContent).toContain("Khu trọ Một")
    expect(alert.textContent).toContain("công việc đang mở")
    await waitFor(() => expect(
      (within(dialog).getByRole("checkbox", { name: /Khu trọ Một/ }) as HTMLInputElement).checked,
    ).toBe(true))
    expect((within(dialog).getByRole("checkbox", { name: /Khu trọ Hai/ }) as HTMLInputElement).checked).toBe(true)
    expect(calls.some((call) => call.url.endsWith("/properties/21/managers/9") && call.init?.method === "DELETE")).toBe(true)
    expect(
      calls.some((call) => call.url.endsWith("/properties/22/managers") && call.init?.method === "POST"),
      JSON.stringify(calls.map((call) => ({ url: call.url, method: call.init?.method }))),
    ).toBe(true)
  })

  it("shows live multi-manager assignments as read-only on the property page", async () => {
    adminSession()
    const property = {
      propertyId: 21,
      ownerId: 7,
      ownerName: admin.fullName,
      propertyName: "Khu trọ có thật",
      addressText: "Địa chỉ thật",
      ward: "Phường 1",
      district: "Thủ Đức",
      city: "TP. Hồ Chí Minh",
      roomCount: 4,
    }
    const assignments = [
      {
        propertyId: 21,
        propertyName: property.propertyName,
        managerId: 9,
        managerName: manager.fullName,
        managerEmail: manager.email,
        managerIsActive: true,
      },
      {
        propertyId: 21,
        propertyName: property.propertyName,
        managerId: 10,
        managerName: "Manager Thứ Hai",
        managerEmail: "manager-two@example.test",
        managerIsActive: true,
      },
    ]
    const calls: Array<{ url: string; init?: RequestInit }> = []
    const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
      const url = String(input)
      calls.push({ url, init })
      if (url.endsWith("/users/me")) return jsonResponse(admin)
      if (url.includes("/properties/managers?propertyIds=21")) return jsonResponse([{ propertyId: 21, managers: assignments }])
      if (url.endsWith("/properties")) return jsonResponse([property])
      return jsonResponse({ title: "Not Found" }, 404)
    })
    vi.stubGlobal("fetch", fetchMock)
    renderPage(<ManagementPropertiesPage />)

    expect(await screen.findByText(manager.fullName)).toBeTruthy()
    expect(await screen.findByText("Manager Thứ Hai")).toBeTruthy()
    expect(screen.queryByText("manager@example.test")).toBeNull()
    expect(screen.queryByText("manager-two@example.test")).toBeNull()
    expect(screen.getByText("Chủ sở hữu: Admin Thật")).toBeTruthy()
    expect(screen.queryByLabelText("Chọn nhiều Manager")).toBeNull()
    expect(screen.queryByRole("button", { name: "Gán Manager" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Thu hồi" })).toBeNull()
    expect(calls.some((call) => call.init?.method === "POST")).toBe(false)
    expect(calls.some((call) => call.init?.method === "DELETE")).toBe(false)
  })

  it("loads a Manager's own properties only from the assigned-property endpoint", async () => {
    managerSession()
    const property = {
      propertyId: 31,
      ownerId: 7,
      ownerName: admin.fullName,
      propertyName: "Khu trọ đã giao",
      addressText: "Địa chỉ được giao",
      ward: "",
      district: "",
      city: "",
      roomCount: 3,
    }
    const calls: string[] = []
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async (input) => {
      const url = String(input)
      calls.push(url)
      if (url.endsWith("/users/me")) return jsonResponse(manager)
      if (url.endsWith("/properties/my-managed")) return jsonResponse([property])
      return jsonResponse({ title: "Not Found" }, 404)
    }))

    renderManagerPage(<ManagementPropertiesPage />)

    expect(await screen.findByText("Khu trọ đã giao")).toBeTruthy()
    expect(screen.getByText(/Bạn \(Manager Thật\) được phân công quản lý khu trọ này/)).toBeTruthy()
    expect(calls.some((url) => url.endsWith("/properties/my-managed"))).toBe(true)
    expect(calls.some((url) => url.endsWith("/properties"))).toBe(false)
  })
})
