import type { ReactNode } from "react"
import { fireEvent, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementReportsPage from "@/pages/ManagementReportsPage/ManagementReportsPage"
import { formatVnd } from "@/utils/publicFormatting"

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children, title, description }: { children: ReactNode; title: string; description: string }) =>
    <main><h1>{title}</h1><p>{description}</p>{children}</main>,
}))

const admin = { userId: 7, fullName: "Admin Thật", email: "admin@example.test", roleId: 3, roleName: "Admin", isActive: true }
const rooms = [{ roomId: 1, status: "Occupied" }, { roomId: 2, status: "Occupied" }, { roomId: 3, status: "Available" }]
const invoices = [
  { invoiceId: 1, invoiceMonth: "2026-11-01", amountPaid: 1_500_000, total: 4_000_000, status: "Paid" },
  { invoiceId: 2, invoiceMonth: "2026-11-01", amountPaid: 500_000, total: 3_000_000, status: "Unpaid" },
  { invoiceId: 3, invoiceMonth: "2026-10-01", amountPaid: 0, total: 2_000_000, status: "Overdue" },
  { invoiceId: 4, invoiceMonth: "2026-10-01", amountPaid: 3_000_000, status: "Paid" },
  { invoiceId: 5, invoiceMonth: "2026-04-01", amountPaid: 9_000_000, status: "Paid" },
  { invoiceId: 0, invoiceMonth: "2026-11-01", amountPaid: 1234, status: "Preview" },
  { invoiceId: 7, invoiceMonth: "2026-12-01", amountPaid: 10_000_000, status: "Paid" },
]
const maintenance = [{ requestId: 1, status: "Completed" }, { requestId: 2, status: "Completed" }, { requestId: 3, status: "Pending" }]
function contract(contractId: number, endDate: string, status = "Active", startDate = "2026-01-01") {
  return { contractId, propertyName: `Khu trọ hợp đồng ${contractId}`, roomCode: `P${contractId}`, customerName: `Khách ${contractId}`, startDate, endDate, status }
}
const contracts = [contract(1, "2026-11-11"), contract(2, "2026-11-20"), contract(3, "2026-12-11"),
  contract(4, "2026-12-12"), contract(5, "2026-11-10"), contract(6, "2026-11-15", "Terminated"),
  contract(7, "2026-11-14", "Active", "2026-11-13"), contract(8, "invalid")]

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } })
}
function api(options: { empty?: boolean; failed?: () => boolean; failStatus?: number; overrides?: Record<string, unknown> } = {}) {
  const responses: Record<string, unknown> = {
    "/api/rooms": rooms, "/api/invoices": invoices,
    "/api/maintenance-requests": maintenance, "/api/contracts": contracts,
    ...options.overrides,
  }
  const fetchMock = vi.fn<typeof fetch>(async (input) => {
    const path = new URL(String(input), window.location.origin).pathname
    if (path === "/api/users/me") return json(admin)
    if (path === "/api/invoices" && options.failed?.()) return json({ title: "Unavailable" }, options.failStatus ?? 503)
    return path in responses ? json(options.empty ? [] : responses[path]) : json({ title: "Not found" }, 404)
  })
  vi.stubGlobal("fetch", fetchMock)
  return fetchMock
}
function renderReports() {
  sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({
    accessToken: "test.reports.token", expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
  }))
  return render(<MemoryRouter><AuthProvider><ProtectedRoute roles={["ADMIN"]}>
    <ManagementReportsPage />
  </ProtectedRoute></AuthProvider></MemoryRouter>)
}
function value(label: string) { return screen.getByText(label).nextElementSibling?.textContent }
beforeEach(() => { sessionStorage.clear(); vi.setSystemTime(new Date("2026-11-11T02:00:00Z")) })
afterEach(() => { vi.useRealTimers() })

describe("management reports API data", () => {
  it("uses received payments rather than invoice face values, and loads all report data from authenticated APIs", async () => {
    const fetchMock = api()
    renderReports()
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    expect(value("Doanh thu tháng")).toBe("2 triệu")
    expect(value("Tỷ lệ lấp đầy")).toBe("66,7%")
    expect(value("Hóa đơn còn nợ")).toBe("2")
    expect(value("Bảo trì hoàn thành")).toBe("2")
    expect(screen.queryByText(/mock|624,5|Sunrise/)).toBeNull()
    expect(screen.getByText("05/2026")).toBeTruthy()
    expect(screen.getByText("11/2026")).toBeTruthy()
    expect(screen.getByLabelText("10/2026: 3.000.000 ₫")).toBeTruthy()
    expect(screen.getByLabelText("11/2026: 2.000.000 ₫")).toBeTruthy()
    const novemberBar = screen.getByLabelText("11/2026: 2.000.000 ₫").querySelector<HTMLElement>("div > div")
    expect(parseFloat(novemberBar?.style.height ?? "")).toBeCloseTo(200 / 3)
    expect(screen.getByLabelText("10/2026: 3.000.000 ₫").querySelector<HTMLElement>("div > div")?.style.height).toBe("100%")
    for (const path of ["/rooms", "/invoices", "/maintenance-requests", "/contracts"]) {
      const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith(path))
      expect(call).toBeTruthy()
      expect(new Headers(call?.[1]?.headers).get("Authorization")).toBe("Bearer test.reports.token")
      expect(call?.[1]?.signal).toBeInstanceOf(AbortSignal)
      expect(call?.[1]?.cache).toBe("no-store")
    }
  })

  it("includes only active contracts ending today through 30 days and shows actual days remaining", async () => {
    api()
    renderReports()
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    expect(screen.getByText("Hết hạn hôm nay")).toBeTruthy()
    expect(screen.getByText("9 ngày")).toBeTruthy()
    expect(screen.getByText("30 ngày")).toBeTruthy()
    for (const id of [4, 5, 6, 7, 8]) expect(screen.queryByText(`Khu trọ hợp đồng ${id} · P${id}`)).toBeNull()
  })

  it("uses the Vietnamese business date and handles the year boundary", async () => {
    vi.setSystemTime(new Date("2026-12-31T18:00:00Z")) // 01/01/2027 in Vietnam.
    api({ overrides: {
      "/api/invoices": [{ invoiceId: 1, invoiceMonth: "2027-01-01", amountPaid: 1_000_000, status: "Paid" }],
      "/api/contracts": [contract(1, "2027-01-01")],
    } })
    renderReports()
    await screen.findByText("Hết hạn hôm nay")
    expect(value("Doanh thu tháng")).toBe("1 triệu")
    expect(screen.getByText("07/2026")).toBeTruthy()
    expect(screen.getByText("01/2027")).toBeTruthy()
  })

  it("shows successful empty states without fake bars, contracts or invalid percentages", async () => {
    api({ empty: true })
    renderReports()
    await screen.findByText("Không có hợp đồng sắp hết hạn trong 30 ngày tới.")
    expect(value("Tỷ lệ lấp đầy")).toBe("0%")
    expect(value("Hóa đơn còn nợ")).toBe("0")
    expect(value("Bảo trì hoàn thành")).toBe("0")
    expect(screen.getByText("Chưa ghi nhận tiền đã thu trong 7 kỳ hóa đơn gần nhất.")).toBeTruthy()
    expect(screen.queryByText(/NaN|Infinity|624,5/)).toBeNull()
    expect(screen.getByLabelText("11/2026: 0 ₫").querySelector<HTMLElement>("div > div")?.style.height).toBe("0%")
  })

  it("shows an error instead of fabricated report numbers, and retries against the backend", async () => {
    let failed = true
    api({ failed: () => failed })
    renderReports()
    await screen.findByRole("alert")
    expect(screen.queryByText("Doanh thu tháng")).toBeNull()
    failed = false
    fireEvent.click(screen.getByRole("button", { name: "Tải lại báo cáo" }))
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    expect(value("Doanh thu tháng")).toBe("2 triệu")
  })

  it("rejects forbidden access without displaying cached or mock figures", async () => {
    api({ failed: () => true, failStatus: 403 })
    renderReports()
    expect((await screen.findByRole("alert")).textContent).toContain("Bạn không có quyền xem báo cáo.")
    expect(screen.queryByText("Tỷ lệ lấp đầy")).toBeNull()
  })

  it("keeps missing payment amounts at zero, ignores invalid periods, and does not fabricate revenue", async () => {
    api({ overrides: { "/api/invoices": [
      { invoiceId: 1, invoiceMonth: "2026-11-01", total: 9_000_000, status: "Paid" },
      { invoiceId: 2, invoiceMonth: "not-a-date", amountPaid: 5_000_000, status: "Paid" },
      { invoiceId: 3, invoiceMonth: "2026-11-01", amountPaid: -100, status: "Paid" },
      { invoiceId: 4, invoiceMonth: "2026-11-01", amountPaid: 250_000, status: "Paid" },
    ] } })
    renderReports()
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    expect(value("Doanh thu tháng")).toBe(formatVnd(250_000))
    expect(value("Hóa đơn còn nợ")).toBe("0")
  })

  it("counts the expiry window in calendar days across leap February", async () => {
    vi.setSystemTime(new Date("2028-02-01T02:00:00Z"))
    api({ overrides: { "/api/contracts": [
      contract(1, "2028-02-29"), contract(2, "2028-03-02"),
      contract(3, "2028-03-03"), contract(4, "2028-02-30"),
    ] } })
    renderReports()
    await screen.findByText("Khu trọ hợp đồng 1 · P1")
    expect(screen.getByText("28 ngày")).toBeTruthy()
    expect(screen.getByText("30 ngày")).toBeTruthy()
    expect(screen.queryByText("Khu trọ hợp đồng 3 · P3")).toBeNull()
    expect(screen.queryByText("Khu trọ hợp đồng 4 · P4")).toBeNull()
  })

  it("refreshes from the backend instead of reusing a static report", async () => {
    const fetchMock = api()
    const view = renderReports()
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    fireEvent.click(screen.getByRole("button", { name: "Làm mới báo cáo" }))
    await screen.findByText("Khu trọ hợp đồng 2 · P2")
    const calls = fetchMock.mock.calls.filter(([url]) => String(url).endsWith("/contracts"))
    expect(calls).toHaveLength(2)
    view.unmount()
    expect(calls.every(([, init]) => init?.signal?.aborted)).toBe(true)
  })
})
