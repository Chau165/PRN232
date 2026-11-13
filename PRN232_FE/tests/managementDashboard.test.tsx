import type { ReactNode } from "react"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementDashboardPage from "@/pages/ManagementDashboardPage/ManagementDashboardPage"

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children, title }: { children: ReactNode; title: string }) => <main><h1>{title}</h1>{children}</main>,
}))

const admin = { userId: 7, fullName: "Admin Thật", email: "admin@example.test", roleId: 3, roleName: "Admin", isActive: true }
const properties = [
  { propertyId: 21, propertyName: "Khu trọ từ database", ownerName: "Chủ thật", district: "Quận 7", city: "TP.HCM", roomCount: 3 },
  { propertyId: 22, propertyName: "Khu trọ thứ hai", ownerName: "Chủ thật", district: "Gò Vấp", city: "TP.HCM", roomCount: 2 },
]
const rooms = [
  { roomId: 1, propertyId: 21, roomCode: "A1", propertyName: properties[0].propertyName, status: "Available" },
  { roomId: 2, propertyId: 21, roomCode: "A2", propertyName: properties[0].propertyName, status: "Occupied" },
  { roomId: 3, propertyId: 21, roomCode: "A3", propertyName: properties[0].propertyName, status: "Reserved" },
  { roomId: 4, propertyId: 22, roomCode: "B1", propertyName: properties[1].propertyName, status: "Occupied" },
  { roomId: 5, propertyId: 22, roomCode: "B2", propertyName: properties[1].propertyName, status: "Maintenance" },
]
const invoices = [{ invoiceId: 1, status: "Paid" }, { invoiceId: 2, status: "Unpaid" }, { invoiceId: 3, status: "Overdue" }]
const maintenance = [
  { requestId: 1, roomId: 5, roomCode: "B2", description: "Vòi nước cần sửa từ API", status: "Pending" },
  { requestId: 2, roomId: 2, roomCode: "A2", description: "Đã xử lý xong", status: "Completed" },
]

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } })
}

function api(options: { empty?: boolean; failed?: () => boolean; failStatus?: number; manager?: boolean; overrides?: Record<string, unknown> } = {}) {
  const responses: Record<string, unknown> = {
    "/api/properties": properties, "/api/rooms": rooms,
    "/api/invoices": invoices, "/api/maintenance-requests": maintenance,
    ...options.overrides,
  }
  const fetchMock = vi.fn<typeof fetch>(async (input) => {
    const path = new URL(String(input), window.location.origin).pathname
    if (path === "/api/users/me") return json(options.manager ? { ...admin, roleName: "Manager", roleId: 2 } : admin)
    if (path === "/api/invoices" && options.failed?.()) return json({ title: "Unavailable" }, options.failStatus ?? 503)
    return path in responses ? json(options.empty ? [] : responses[path]) : json({ title: "Not found" }, 404)
  })
  vi.stubGlobal("fetch", fetchMock)
  return fetchMock
}

function renderDashboard() {
  sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({
    accessToken: "test.dashboard.token", expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
  }))
  return render(<MemoryRouter><AuthProvider><ProtectedRoute roles={["ADMIN", "MANAGER"]}>
    <ManagementDashboardPage />
  </ProtectedRoute></AuthProvider></MemoryRouter>)
}

function value(label: string) { return screen.getByText(label).nextElementSibling?.textContent }

beforeEach(() => { sessionStorage.clear() })

describe("management dashboard API data", () => {
  it("loads every statistic, property and maintenance attention item from authenticated backend requests", async () => {
    const fetchMock = api()
    renderDashboard()
    await screen.findByText("Khu trọ từ database")
    expect(value("Tổng khu trọ")).toBe("2")
    expect(value("Tổng phòng")).toBe("5")
    expect(value("Phòng trống")).toBe("1")
    expect(value("Đang thuê")).toBe("2")
    expect(value("Đang sửa chữa")).toBe("1")
    expect(value("Hóa đơn chưa thanh toán")).toBe("2")
    expect(screen.getByText("20% tổng số phòng")).toBeTruthy()
    expect(screen.getByText("40% tổng số phòng")).toBeTruthy()
    expect(screen.getByText("Vòi nước cần sửa từ API")).toBeTruthy()
    expect(screen.queryByText("Đã xử lý xong")).toBeNull()
    expect(screen.queryByText("Sunrise Residence")).toBeNull()
    for (const path of ["/properties", "/rooms", "/invoices", "/maintenance-requests"]) {
      const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith(path))
      expect(call).toBeTruthy()
      expect(new Headers(call?.[1]?.headers).get("Authorization")).toBe("Bearer test.dashboard.token")
      expect(call?.[1]?.signal).toBeInstanceOf(AbortSignal)
      expect(call?.[1]?.cache).toBe("no-store")
    }
  })

  it("shows zero and meaningful empty states only after successful empty API responses", async () => {
    api({ empty: true })
    renderDashboard()
    await screen.findByText("Chưa có khu trọ trong phạm vi quản lý.")
    for (const label of ["Tổng khu trọ", "Tổng phòng", "Phòng trống", "Đang thuê", "Đang sửa chữa", "Hóa đơn chưa thanh toán"]) {
      expect(value(label)).toBe("0")
    }
    expect(screen.getByText("Không có yêu cầu bảo trì cần xử lý.")).toBeTruthy()
    expect(screen.queryByText(/NaN|Infinity/)).toBeNull()
  })

  it("does not show mock or misleading zero statistics when an API fails, and supports retry", async () => {
    let failed = true
    api({ failed: () => failed })
    renderDashboard()
    await screen.findByRole("alert")
    expect(screen.queryByText("Tổng phòng")).toBeNull()
    failed = false
    fireEvent.click(screen.getByRole("button", { name: "Tải lại dashboard" }))
    await screen.findByText("Khu trọ từ database")
    expect(value("Tổng phòng")).toBe("5")
  })

  it("uses backend data for the shared Manager dashboard instead of the mock assignment ids", async () => {
    api({ manager: true })
    renderDashboard()
    await screen.findByText("Khu trọ từ database")
    expect(value("Khu trọ phụ trách")).toBe("2")
    expect(value("Tổng phòng")).toBe("5")
  })

  it("cancels dashboard requests on unmount", async () => {
    const fetchMock = api()
    const view = renderDashboard()
    await screen.findByText("Khu trọ từ database")
    const calls = fetchMock.mock.calls.filter(([url]) => !String(url).endsWith("/users/me"))
    view.unmount()
    await waitFor(() => expect(calls.every(([, init]) => init?.signal?.aborted)).toBe(true))
  })

  it("shows an explicit permission error instead of demo statistics for a forbidden API", async () => {
    api({ failed: () => true, failStatus: 403 })
    renderDashboard()
    expect((await screen.findByRole("alert")).textContent).toContain("Bạn không có quyền xem dữ liệu dashboard.")
    expect(screen.queryByText("Tổng phòng")).toBeNull()
  })

  it("keeps property counts accurate and lists only three unresolved maintenance requests", async () => {
    api({ overrides: {
      "/api/properties": [...properties, { ...properties[0], propertyId: 23, propertyName: "Khu trọ chưa có phòng", roomCount: 0 }],
      "/api/rooms": [...rooms, { ...rooms[0], roomId: 99, propertyId: 999 }],
      "/api/invoices": [...invoices, { invoiceId: 4, status: "Preview" }],
      "/api/maintenance-requests": [
        { ...maintenance[0], requestId: 1, description: "Việc cũ hơn" },
        { ...maintenance[0], requestId: 2, description: "Việc đang chờ" },
        { ...maintenance[0], requestId: 3, description: "Việc đã nhận", status: "Approved" },
        { ...maintenance[0], requestId: 4, roomId: 404, roomCode: null, description: "Việc không còn thông tin phòng" },
        { ...maintenance[0], requestId: 5, status: "Completed", description: "Việc đã xong" },
      ],
    } })
    renderDashboard()
    await screen.findByText("Khu trọ chưa có phòng")
    expect(value("Tổng khu trọ")).toBe("3")
    expect(value("Tổng phòng")).toBe("5")
    expect(value("Hóa đơn chưa thanh toán")).toBe("2")
    expect(screen.getByText("0/0 đang thuê")).toBeTruthy()
    expect(screen.getByText("Đã phân công")).toBeTruthy()
    expect(screen.getByText("Khu trọ chưa xác định · Phòng chưa xác định")).toBeTruthy()
    expect(screen.queryByText("Việc cũ hơn")).toBeNull()
    expect(screen.queryByText("Việc đã xong")).toBeNull()
  })

  it("shows loading, not mock values or partial statistics, while the backend request is pending", async () => {
    let resolveInvoices: ((response: Response) => void) | undefined
    const fallback = api()
    vi.stubGlobal("fetch", vi.fn<typeof fetch>((input, init) => {
      if (String(input).endsWith("/invoices")) return new Promise((resolve) => { resolveInvoices = resolve })
      return fallback(input, init)
    }))
    renderDashboard()
    await screen.findByText("Đang tải dashboard...")
    expect(screen.queryByText("Tổng phòng")).toBeNull()
    await waitFor(() => expect(resolveInvoices).toBeTruthy())
    resolveInvoices?.(json(invoices))
    await screen.findByText("Khu trọ từ database")
    expect(value("Tổng phòng")).toBe("5")
  })
})
