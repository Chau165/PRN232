import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, expect, it, vi } from "vitest"
import ManagementReportsPage from "@/pages/ManagementReportsPage/ManagementReportsPage"

const mocks = vi.hoisted(() => ({ revenue: vi.fn(), debt: vi.fn() }))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => ({ accessToken: "manager-token" }) }))
vi.mock("@/services/reportService", () => ({ getRevenueReport: mocks.revenue, getReceivablesReport: mocks.debt }))
vi.mock("@/components/management/ManagementPage", () => ({ default: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }))

beforeEach(() => {
  mocks.revenue.mockReset().mockResolvedValue({ totalCollected: 3355000, paymentCount: 1, unknownDateAmount: 500, unknownDatePaymentCount: 1,
    byProperty: [{ propertyId: 1, propertyName: "Sunrise", collectedAmount: 3355000, paymentCount: 1, unknownDateAmount: 500, unknownDatePaymentCount: 1 }] })
  mocks.debt.mockReset().mockResolvedValue({ asOfDate: "2026-11-11", outstandingAmount: 0, outstandingInvoiceCount: 0, overdueAmount: 0, overdueInvoiceCount: 0 })
})

it("renders real invoice totals, unknown-date transactions and property grouping without mock revenue", async () => {
  render(<ManagementReportsPage />)
  await screen.findByText("1 giao dịch đã ghi nhận")
  expect(screen.getAllByText(/3\.355\.000/).length).toBeGreaterThan(0)
  expect(screen.getByText("Sunrise")).toBeTruthy()
  expect(screen.getByText("1 giao dịch cần kiểm tra, chưa phân bổ vào tháng")).toBeTruthy()
  expect(screen.queryByText("624,5 triệu")).toBeNull()
  fireEvent.change(screen.getByLabelText("Tháng doanh thu"), { target: { value: "2026-10" } })
  await waitFor(() => expect(mocks.revenue).toHaveBeenLastCalledWith("manager-token", "2026-10", expect.any(AbortSignal)))
})

it("does not show fabricated zeros or old values when a report request fails", async () => {
  mocks.revenue.mockRejectedValue(new Error("Network error"))
  render(<ManagementReportsPage />)
  await screen.findByRole("alert")
  expect(screen.queryByText("1 giao dịch đã ghi nhận")).toBeNull()
  mocks.revenue.mockResolvedValue({ totalCollected: 0, paymentCount: 0, unknownDateAmount: 0, unknownDatePaymentCount: 0, byProperty: [] })
  fireEvent.click(screen.getByRole("button", { name: "Tải lại báo cáo" }))
  await screen.findByText("0 giao dịch đã ghi nhận")
})
