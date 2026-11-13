import type { ReactNode } from "react"
import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { MemoryRouter } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import ManagementReportsPage from "@/pages/ManagementReportsPage/ManagementReportsPage"
import { formatVnd } from "@/utils/publicFormatting"

vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children, title }: { children: ReactNode; title: string }) => <main><h1>{title}</h1>{children}</main>,
}))
const revenue = { totalCollected: 3355000, paymentCount: 1, unknownDateAmount: 75000, unknownDatePaymentCount: 2,
  byProperty: [{ propertyId: 21, propertyName: "Khu trọ API", collectedAmount: 3355000,
    paymentCount: 1, unknownDateAmount: 75000, unknownDatePaymentCount: 2 }] }
const debt = { asOfDate: "2026-11-11", outstandingAmount: 900000, outstandingInvoiceCount: 3,
  overdueAmount: 200000, overdueInvoiceCount: 1 }
function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } })
}
function api(options: { manager?: boolean; status?: () => number; revenue?: () => typeof revenue;
  onReport?: (url: URL, init?: RequestInit) => Promise<Response> | undefined } = {}) {
  const mock = vi.fn<typeof fetch>(async (input, init) => {
    const url = new URL(String(input), window.location.origin)
    if (url.pathname === "/api/users/me") return json({ userId: 7, fullName: "Người xem báo cáo",
      email: "reports@example.test", roleId: options.manager ? 2 : 3,
      roleName: options.manager ? "Manager" : "Admin", isActive: true })
    const intercepted = options.onReport?.(url, init)
    if (intercepted) return intercepted
    const status = options.status?.() ?? 200
    if (status !== 200) return json({ title: "Unavailable" }, status)
    if (url.pathname === "/api/reports/revenue") return json(options.revenue?.() ?? revenue)
    if (url.pathname === "/api/reports/receivables") return json(debt)
    if (url.pathname === "/api/reports/booking-deposits") return json({ totals: { collectedAmount: 0, collectionCount: 0, refundedAmount: 0, refundCount: 0, netCashFlow: 0 }, unknownDate: { collectedAmount: 0, collectionCount: 0, refundedAmount: 0, refundCount: 0 }, byProperty: [] })
    return json({ title: "Unexpected endpoint" }, 404)
  })
  vi.stubGlobal("fetch", mock)
  return mock
}
function renderReports() {
  sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({ accessToken: "test.reports.token",
    expiresAtUtc: new Date(Date.now() + 60000).toISOString() }))
  return render(<MemoryRouter><AuthProvider><ProtectedRoute roles={["ADMIN", "MANAGER"]}>
    <ManagementReportsPage />
  </ProtectedRoute></AuthProvider></MemoryRouter>)
}
function value(label: string) { return screen.getByText(label).nextElementSibling?.textContent }
beforeEach(() => { sessionStorage.clear(); vi.setSystemTime(new Date("2026-11-11T02:00:00Z")) })
afterEach(() => { vi.useRealTimers() })

describe("management financial reports", () => {
  it.each([false, true])("loads server reports for manager=%s without summing invoice periods", async manager => {
    const fetchMock = api({ manager })
    renderReports()
    await screen.findByText("Khu trọ API")
    expect(value("Doanh thu tháng")).toBe(formatVnd(revenue.totalCollected))
    expect(value("Tổng công nợ")).toBe(formatVnd(debt.outstandingAmount))
    expect(value("Nợ quá hạn")).toBe(formatVnd(debt.overdueAmount))
    expect(value("Chưa xác định thời gian thu")).toBe(formatVnd(revenue.unknownDateAmount))
    expect(screen.getByText("1 giao dịch đã ghi nhận")).toBeTruthy()
    const reports = fetchMock.mock.calls.filter(([url]) => String(url).includes("/reports/"))
    expect(reports).toHaveLength(3)
    for (const [, init] of reports) {
      expect(new Headers(init?.headers).get("Authorization")).toBe("Bearer test.reports.token")
      expect(init?.signal).toBeInstanceOf(AbortSignal)
      expect(init?.cache).toBe("no-store")
    }
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/invoices"))).toBe(false)
  })
  it("filters revenue by selected month while retaining the debt as-of date", async () => {
    const fetchMock = api()
    renderReports()
    await screen.findByText("Khu trọ API")
    fireEvent.change(screen.getByLabelText("Tháng doanh thu"), { target: { value: "2026-10" } })
    await screen.findByText("Doanh thu theo khu trọ · 2026-10")
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("fromMonth=2026-10-01&toMonth=2026-10-01"))).toBe(true)
    expect(screen.getByText(/đến 2026-11-11/)).toBeTruthy()
  })
  it("uses the Vietnamese month across the year boundary", async () => {
    vi.setSystemTime(new Date("2026-12-31T18:00:00Z"))
    const fetchMock = api()
    renderReports()
    await screen.findByText("Khu trọ API")
    expect((screen.getByLabelText("Tháng doanh thu") as HTMLInputElement).value).toBe("2027-01")
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes("fromMonth=2027-01-01"))).toBe(true)
  })
  it("renders an empty result without fabricated revenue", async () => {
    api({ revenue: () => ({ ...revenue, totalCollected: 0, paymentCount: 0, unknownDateAmount: 0,
      unknownDatePaymentCount: 0, byProperty: [] }) })
    renderReports()
    await screen.findByText("Chưa có doanh thu thuộc phạm vi được xem.")
    expect(value("Doanh thu tháng")).toBe(formatVnd(0))
  })
  it("shows request errors and reloads both reports", async () => {
    let status = 503
    const fetchMock = api({ status: () => status })
    renderReports()
    await screen.findByText("Không thể tải báo cáo từ máy chủ. Vui lòng thử lại.")
    expect(screen.queryByText("Doanh thu tháng")).toBeNull()
    status = 200
    fireEvent.click(screen.getByRole("button", { name: "Tải lại báo cáo" }))
    await screen.findByText("Khu trọ API")
    expect(fetchMock.mock.calls.filter(([url]) => String(url).includes("/reports/")).length).toBeGreaterThanOrEqual(4)
  })
  it("shows forbidden access without financial figures", async () => {
    api({ status: () => 403 })
    renderReports()
    expect(await screen.findByText("Bạn không có quyền xem báo cáo.")).toHaveProperty("textContent", "Bạn không có quyền xem báo cáo.")
    expect(screen.queryByText("Doanh thu tháng")).toBeNull()
  })
  it("refreshes server totals instead of keeping the old snapshot", async () => {
    let amount = 100
    api({ revenue: () => ({ ...revenue, totalCollected: amount }) })
    renderReports()
    await screen.findByText("Khu trọ API")
    amount = 200
    fireEvent.click(screen.getByRole("button", { name: "Tải lại báo cáo" }))
    await waitFor(() => expect(value("Doanh thu tháng")).toBe(formatVnd(200)))
  })
  it("aborts both pending requests on unmount", async () => {
    const signals: AbortSignal[] = []
    api({ onReport: (url, init) => {
      if (!url.pathname.includes("/reports/")) return undefined
      signals.push(init!.signal!)
      return new Promise<Response>(() => {})
    } })
    const view = renderReports()
    await waitFor(() => expect(signals).toHaveLength(3))
    view.unmount()
    expect(signals.every(signal => signal.aborted)).toBe(true)
  })
})
