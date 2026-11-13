import { act, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import InvoicePaymentResultPage from "@/pages/InvoicePaymentResultPage/InvoicePaymentResultPage"
import PayInvoiceModal from "@/components/customer/PayInvoiceModal"
import type { PaymentRequestStatusDto } from "@/types/invoicePayment"
import type { InvoiceDto } from "@/types/invoice"
import { recalledInvoicePayment, rememberInvoicePayment, validPayOSCheckout } from "@/utils/invoicePayment"

const mocks = vi.hoisted(() => ({ get: vi.fn(), create: vi.fn(), open: vi.fn() }))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => ({ accessToken: "token", user: { id: "10", role: "CUSTOMER" } }) }))
vi.mock("@/services/invoicePaymentService", () => ({ getPaymentRequest: mocks.get, createInvoicePaymentLink: mocks.create }))
vi.mock("@/utils/invoicePayment", async importOriginal => ({
  ...await importOriginal<typeof import("@/utils/invoicePayment")>(), openPayOSCheckout: mocks.open,
}))
const status = (changes: Partial<PaymentRequestStatusDto> = {}): PaymentRequestStatusDto => ({
  paymentRequestId: 12, invoiceId: 5, customerId: 10, propertyId: 1, status: "Pending", invoiceStatus: "Unpaid",
  amount: 3355000, currency: "VND", createdAtUtc: "2026-11-01T00:00:00Z", updatedAtUtc: "2026-11-01T00:00:00Z",
  expiresAtUtc: "2099-01-01T00:00:00Z", confirmedAtUtc: null, paymentId: null, paidAtUtc: null,
  isExpired: false, canOpenCheckout: true, checkoutUrl: "https://pay.payos.vn/web/link-123", ...changes,
})
const invoice: InvoiceDto = { invoiceId: 5, contractId: 1, roomCode: "A101", propertyName: "Sunrise", customerName: "An",
  invoiceMonth: "2026-11-01", dueDate: "2026-12-10", status: "Unpaid", total: 3355000, lines: [] }
const result = (url = "/payment-result?requestId=12") => render(<MemoryRouter initialEntries={[url]}><InvoicePaymentResultPage /></MemoryRouter>)

beforeEach(() => { sessionStorage.clear(); mocks.get.mockReset(); mocks.create.mockReset(); mocks.open.mockReset() })
afterEach(() => vi.useRealTimers())

describe("payOS invoice checkout", () => {
  it("opens a valid existing checkout once without calling the simulated pay API", async () => {
    mocks.create.mockResolvedValue({ ...status(), reused: true })
    render(<MemoryRouter><PayInvoiceModal invoice={invoice} onClose={vi.fn()} /></MemoryRouter>)
    fireEvent.click(screen.getByRole("button", { name: "Thanh toán qua payOS" }))
    fireEvent.click(screen.getByRole("button", { name: "Đang tạo liên kết..." }))
    await waitFor(() => expect(mocks.open).toHaveBeenCalledWith("https://pay.payos.vn/web/link-123"))
    expect(mocks.create).toHaveBeenCalledTimes(1)
    expect(mocks.create).toHaveBeenCalledWith("token", 5)
    expect(recalledInvoicePayment("10")).toEqual({ requestId: 12, invoiceId: 5, providerLinkId: "link-123" })
    expect(screen.queryByText("Thanh toán thành công")).toBeNull()
    expect(screen.queryByLabelText("MoMo")).toBeNull()
  })

  it("moves a Creating request to the status page instead of posting another creation", async () => {
    mocks.create.mockResolvedValue({ ...status(), status: "Creating", checkoutUrl: null })
    mocks.get.mockResolvedValue(status({ status: "Creating", checkoutUrl: null, canOpenCheckout: false }))
    render(<MemoryRouter initialEntries={["/pay"]}><Routes>
      <Route path="/pay" element={<PayInvoiceModal invoice={invoice} onClose={vi.fn()} />} />
      <Route path="/payment-result" element={<InvoicePaymentResultPage />} />
    </Routes></MemoryRouter>)
    fireEvent.click(screen.getByRole("button", { name: "Thanh toán qua payOS" }))
    await screen.findByRole("heading", { name: "Đang tạo liên kết thanh toán" })
    expect(mocks.create).toHaveBeenCalledTimes(1)
    expect(mocks.open).not.toHaveBeenCalled()
  })

  it.each([409, 503])("shows a recoverable creation error for HTTP %s without announcing success", async code => {
    mocks.create.mockRejectedValue(new ApiError("Provider error", { status: code }))
    render(<MemoryRouter><PayInvoiceModal invoice={invoice} onClose={vi.fn()} /></MemoryRouter>)
    fireEvent.click(screen.getByRole("button", { name: "Thanh toán qua payOS" }))
    await screen.findByRole("alert")
    expect(mocks.open).not.toHaveBeenCalled()
    expect(screen.queryByText("Thanh toán thành công")).toBeNull()
  })
})

describe("payment result verification", () => {
  it("ignores forged success/cancel query flags and only shows BE-confirmed success", async () => {
    mocks.get.mockResolvedValue(status())
    result("/payment-result?requestId=12&status=PAID&code=00&cancel=false")
    await screen.findByRole("heading", { name: "Đang chờ xác nhận thanh toán" })
    expect(screen.queryByRole("heading", { name: "Thanh toán thành công" })).toBeNull()
    expect(screen.getByRole("link", { name: "Mở checkout payOS" }).getAttribute("href")).toBe("https://pay.payos.vn/web/link-123")
  })

  it("restores same-tab checkout context after a provider callback and refresh", async () => {
    rememberInvoicePayment("10", 12, 5, "link-123")
    mocks.get.mockResolvedValue(status({ status: "Succeeded", invoiceStatus: "Paid", canOpenCheckout: false, checkoutUrl: null }))
    result("/payment-result?id=link-123&status=CANCELLED&cancel=true")
    await screen.findByRole("heading", { name: "Thanh toán thành công" })
    expect(mocks.get).toHaveBeenCalledWith("token", 12, expect.any(AbortSignal))
    expect(screen.queryByRole("link", { name: "Mở checkout payOS" })).toBeNull()
  })

  it.each(["/payment-result", "/payment-result?requestId=NaN", "/payment-result?requestId=-1", "/payment-result?id=wrong-link"]) (
    "does not guess a request when context is missing or invalid: %s", url => {
      if (url.includes("wrong-link")) rememberInvoicePayment("10", 12, 5, "link-123")
      result(url)
      expect(screen.getByRole("heading", { name: "Chưa xác định được yêu cầu thanh toán" })).toBeTruthy()
      expect(mocks.get).not.toHaveBeenCalled()
    })

  it.each([
    ["Cancelled", "Liên kết đã hủy"], ["Expired", "Liên kết đã hết hạn"],
    ["Create-failed", "Chưa tạo được liên kết"], ["RequiresReview", "Thanh toán cần kiểm tra"],
  ] as const)("displays terminal %s without offering another checkout", async (state, title) => {
    mocks.get.mockResolvedValue(status({ status: state })) // even if a stale/malformed response still exposes a URL
    result()
    await screen.findByRole("heading", { name: title })
    expect(screen.queryByRole("link", { name: "Mở checkout payOS" })).toBeNull()
  })

  it("does not describe a cash-paid invoice as payOS success", async () => {
    mocks.get.mockResolvedValue(status({ status: "Cancelled", invoiceStatus: "Paid" }))
    result()
    await screen.findByRole("heading", { name: "Hóa đơn đã được thanh toán" })
    expect(screen.queryByRole("heading", { name: "Thanh toán thành công" })).toBeNull()
  })

  it("handles forbidden requests without showing another customer's data", async () => {
    mocks.get.mockRejectedValue(new ApiError("Forbidden", { status: 403 }))
    result()
    expect((await screen.findByRole("alert")).textContent).toContain("không có quyền")
    expect(screen.queryByText(/Hóa đơn #5/)).toBeNull()
  })

  it("polls sequentially, stops at confirmed success, and cleans up after leaving", async () => {
    vi.useFakeTimers()
    mocks.get.mockResolvedValueOnce(status()).mockResolvedValue(status({ status: "Succeeded", invoiceStatus: "Paid" }))
    const view = result()
    await act(async () => { await Promise.resolve() })
    expect(mocks.get).toHaveBeenCalledTimes(1)
    await act(async () => { await vi.advanceTimersByTimeAsync(4000) })
    expect(screen.getByRole("heading", { name: "Thanh toán thành công" })).toBeTruthy()
    await act(async () => { await vi.advanceTimersByTimeAsync(120000) })
    expect(mocks.get).toHaveBeenCalledTimes(2)
    view.unmount()
  })

  it("pauses after bounded polling instead of reporting failure or making endless calls", async () => {
    vi.useFakeTimers()
    mocks.get.mockResolvedValue(status({ isExpired: true }))
    const view = result()
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(125000) })
    expect(mocks.get).toHaveBeenCalledTimes(30)
    expect(screen.getByText(/Đã tạm dừng kiểm tra tự động/)).toBeTruthy()
    expect(screen.queryByRole("link", { name: "Mở checkout payOS" })).toBeNull()
    mocks.get.mockImplementationOnce(() => new Promise(() => {}))
    fireEvent.click(screen.getByRole("button", { name: "Kiểm tra lại trạng thái" }))
    await act(async () => { await Promise.resolve() })
    expect(mocks.get).toHaveBeenCalledTimes(31)
    const signal = mocks.get.mock.calls.at(-1)![2] as AbortSignal
    view.unmount()
    expect(signal.aborted).toBe(true)
  })
})

it.each(["javascript:alert(1)", "https://evil.test/web/link", "https://pay.payos.vn.evil.test/web/link", "https://pay.payos.vn@evil.test/web/link", "http://pay.payos.vn/web/link"])("rejects unsafe checkout URLs: %s", url => {
  expect(validPayOSCheckout(url)).toBeNull()
})

it("isolates payment context by account", () => {
  rememberInvoicePayment("10", 12, 5)
  expect(recalledInvoicePayment("20")).toBeNull()
})
