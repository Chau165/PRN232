import { act, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, expect, it, vi } from "vitest"
import DepositReportPanel from "@/components/management/DepositReportPanel"
const api = vi.hoisted(() => ({ get: vi.fn() }))
vi.mock("@/services/reportService", () => ({ getDepositReport: api.get }))
const totals = { collectedAmount: 3000000, collectionCount: 1, refundedAmount: 2500000, refundCount: 1, netCashFlow: 500000 }
const data = { totals, unknownDate: { ...totals, collectedAmount: 200, collectionCount: 2 }, byProperty: [{ propertyId: 1, propertyName: "Linh Trung", totals, unknownDate: totals }] }
beforeEach(() => api.get.mockReset())
afterEach(() => vi.useRealTimers())
it("shows actual collected/refunded cash and unknown dates separately", async () => {
  api.get.mockResolvedValue(data)
  render(<DepositReportPanel token="admin" month="2026-11" version={0} />)
  await screen.findByText("Linh Trung")
  expect(screen.getByText(/Chưa rõ thời điểm, chưa phân bổ/).textContent).toContain("2 giao dịch")
  expect(screen.getByText(/Dòng tiền trong tháng/).textContent).toContain("không phải số dư cọc")
  expect(api.get).toHaveBeenCalledWith("admin", "2026-11", expect.any(AbortSignal))
})
it("ignores stale responses after account or filter changes", async () => {
  let resolve!: (value: typeof data) => void
  api.get.mockReturnValueOnce(new Promise(r => { resolve = r })).mockResolvedValue({ ...data, byProperty: [] })
  const view = render(<DepositReportPanel token="old-manager" month="2026-11" version={0} />)
  const signal = api.get.mock.calls[0][2] as AbortSignal
  view.rerender(<DepositReportPanel token="new-manager" month="2026-12" version={0} />)
  await screen.findByText("Không có giao dịch tiền cọc trong phạm vi được xem.")
  await act(async () => resolve(data))
  expect(signal.aborted).toBe(true)
  expect(screen.queryByText("Linh Trung")).toBeNull()
})
it("times out a stalled read and manual refresh can recover", async () => {
  vi.useFakeTimers()
  api.get.mockReturnValueOnce(new Promise(() => {})).mockResolvedValue(data)
  const view = render(<DepositReportPanel token="admin" month="2026-11" version={0} />)
  await act(async () => vi.advanceTimersByTimeAsync(15000))
  expect(screen.getByRole("alert").textContent).toContain("hết thời gian")
  expect(api.get.mock.calls[0][2].aborted).toBe(true)
  await act(async () => view.rerender(<DepositReportPanel token="admin" month="2026-11" version={1} />))
  expect(screen.getByText("Linh Trung")).toBeTruthy()
})
