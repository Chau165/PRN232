import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import ManagementPaymentReviewsPage from "@/pages/ManagementPaymentReviewsPage/ManagementPaymentReviewsPage"
import type { PaymentReviewDto } from "@/types/paymentReview"
import { formatVnd } from "@/utils/publicFormatting"

const mocks = vi.hoisted(() => ({
  getPaymentReviews: vi.fn(),
  getPaymentReview: vi.fn(),
  resolvePaymentReview: vi.fn(),
  auth: { accessToken: "admin-token", user: { id: "7", role: "ADMIN" } },
}))

vi.mock("@/auth/AuthContext", () => ({ useAuth: () => mocks.auth }))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/paymentReviewService", () => ({
  getPaymentReviews: mocks.getPaymentReviews,
  getPaymentReview: mocks.getPaymentReview,
  resolvePaymentReview: mocks.resolvePaymentReview,
}))

function review(overrides: Partial<PaymentReviewDto> = {}): PaymentReviewDto {
  return {
    id: 12, invoiceId: 9, paymentRequestId: 18, reason: "Hoá đơn đã có khoản thanh toán",
    status: "Open", additionalAmount: 2_000_000, currency: "VND", providerReference: "PAYOS-123456",
    source: "payOS", transactionDateTime: "2026-11-13T03:00:00Z", existingPaymentId: 3,
    existingPaymentMethod: "Cash", existingAmount: 2_000_000, existingPaidAtUtc: "2026-11-12T03:00:00Z",
    createdAtUtc: "2026-11-13T03:00:00Z", resolution: null, resolutionNote: null,
    evidenceReference: null, resolvedByAdminId: null, resolvedAtUtc: null,
    ...overrides,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

function renderPage() {
  return render(<MemoryRouter><ManagementPaymentReviewsPage /></MemoryRouter>)
}

async function openDetails() {
  fireEvent.click(await screen.findByRole("button", { name: "Xem mục đối soát #12" }))
  await screen.findByLabelText("Ghi chú (bắt buộc)")
}

function fillNote(value = "Đã đối chiếu khoản thanh toán") {
  fireEvent.change(screen.getByLabelText("Ghi chú (bắt buộc)"), { target: { value } })
}

beforeEach(() => {
  vi.resetAllMocks()
  mocks.auth = { accessToken: "admin-token", user: { id: "7", role: "ADMIN" } }
  mocks.getPaymentReviews.mockResolvedValue([review()])
  mocks.getPaymentReview.mockResolvedValue(review())
  mocks.resolvePaymentReview.mockResolvedValue(review({
    status: "Resolved", resolution: "NotDuplicate", resolutionNote: "Đã đối chiếu khoản thanh toán",
    resolvedAtUtc: "2026-11-13T04:00:00Z", resolvedByAdminId: 7,
  }))
})

describe("ManagementPaymentReviewsPage", () => {
  it("loads Open by default with 20 items per page", async () => {
    renderPage()
    const row = await screen.findByRole("button", { name: "Xem mục đối soát #12" })
    expect(mocks.getPaymentReviews).toHaveBeenCalledWith("admin-token", { status: "Open", page: 1, pageSize: 20 }, expect.any(AbortSignal))
    expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe("Open")
    expect(within(row).getByText("Hoá đơn #9")).toBeTruthy()
    expect(row.textContent).toContain(formatVnd(2_000_000))
    expect(within(row).getByText("Cần xử lý")).toBeTruthy()
    expect((screen.getByRole("button", { name: "Trước" }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole("button", { name: "Sau" }) as HTMLButtonElement).disabled).toBe(true)
  })

  it("blocks an external refund without evidence", async () => {
    renderPage()
    await openDetails()
    fillNote()
    fireEvent.click(screen.getByRole("radio", { name: "Đã hoàn tiền ngoài hệ thống" }))
    fireEvent.change(screen.getByLabelText("Mã chứng từ hoàn tiền"), { target: { value: "   " } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    expect(mocks.resolvePaymentReview).not.toHaveBeenCalled()
    expect(screen.getByRole("alert").textContent).toContain("Vui lòng nhập mã chứng từ")
  })

  it("sends NotDuplicate with a trimmed note and null evidence", async () => {
    renderPage()
    await openDetails()
    fillNote("  Đã đối chiếu khoản thanh toán  ")
    fireEvent.click(screen.getByRole("radio", { name: "Không phải trả trùng" }))
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    await screen.findByText("Đã xử lý mục đối soát #12.")
    expect(mocks.resolvePaymentReview).toHaveBeenCalledExactlyOnceWith("admin-token", 12, {
      resolution: "NotDuplicate", note: "Đã đối chiếu khoản thanh toán", evidenceReference: null,
    })
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
    await waitFor(() => expect(mocks.getPaymentReviews).toHaveBeenCalledTimes(2))
  })

  it("sends external refund evidence and shows the recorded result", async () => {
    mocks.resolvePaymentReview.mockResolvedValue(review({
      status: "Resolved", resolution: "RefundedExternally", resolutionNote: "Đã hoàn qua ngân hàng",
      evidenceReference: "BANK-123", resolvedAtUtc: "2026-11-13T04:00:00Z",
    }))
    renderPage()
    await openDetails()
    fillNote("Đã hoàn qua ngân hàng")
    fireEvent.change(screen.getByLabelText("Mã chứng từ hoàn tiền"), { target: { value: "  BANK-123  " } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    await screen.findByText("Đã xử lý mục đối soát #12.")
    expect(mocks.resolvePaymentReview).toHaveBeenCalledExactlyOnceWith("admin-token", 12, {
      resolution: "RefundedExternally", note: "Đã hoàn qua ngân hàng", evidenceReference: "BANK-123",
    })
    const panel = screen.getByRole("region", { name: "Chi tiết đối soát thanh toán" })
    expect(within(panel).getByText("Đã hoàn tiền ngoài hệ thống")).toBeTruthy()
    expect(within(panel).getByText("BANK-123")).toBeTruthy()
    expect(within(panel).getByText("Đã hoàn qua ngân hàng")).toBeTruthy()
  })

  it("validates note length after trimming and caps evidence at 256 characters", async () => {
    renderPage()
    await openDetails()
    fireEvent.click(screen.getByRole("radio", { name: "Không phải trả trùng" }))
    for (const invalid of ["", "    ", "  abcd  ", "a".repeat(2001)]) {
      fillNote(invalid)
      fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
      expect(mocks.resolvePaymentReview).not.toHaveBeenCalled()
    }
    fillNote("  " + "a".repeat(2000) + "  ")
    fireEvent.change(screen.getByLabelText("Mã chứng từ hoàn tiền"), { target: { value: "b".repeat(257) } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    expect(mocks.resolvePaymentReview).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText("Mã chứng từ hoàn tiền"), { target: { value: "" } })
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    await screen.findByText("Đã xử lý mục đối soát #12.")
    expect(mocks.resolvePaymentReview).toHaveBeenCalledWith("admin-token", 12, {
      resolution: "NotDuplicate", note: "a".repeat(2000), evidenceReference: null,
    })
  })

  it("prevents duplicate submissions and rereads detail after 409 before allowing another action", async () => {
    const post = deferred<PaymentReviewDto>()
    const reread = deferred<PaymentReviewDto>()
    mocks.resolvePaymentReview.mockReturnValue(post.promise)
    mocks.getPaymentReview.mockResolvedValueOnce(review()).mockReturnValueOnce(reread.promise)
    renderPage()
    await openDetails()
    fillNote()
    fireEvent.click(screen.getByRole("radio", { name: "Không phải trả trùng" }))
    const submit = screen.getByRole("button", { name: "Xác nhận xử lý" })
    fireEvent.click(submit)
    fireEvent.click(submit)
    expect((submit as HTMLButtonElement).disabled).toBe(true)
    expect(mocks.resolvePaymentReview).toHaveBeenCalledTimes(1)
    await act(async () => post.reject(new ApiError("Conflict", { status: 409 })))
    expect(mocks.getPaymentReview).toHaveBeenCalledTimes(2)
    expect(screen.getByRole("alert").textContent).toBe("Mục này vừa được xử lý hoặc đã thay đổi. Vui lòng tải lại.")
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
    await act(async () => reread.resolve(review({ status: "Resolved", resolution: "NotDuplicate" })))
    await waitFor(() => expect(mocks.getPaymentReviews).toHaveBeenCalledTimes(2))
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
  })

  it("keeps the form hidden if rereading after a failed POST also fails", async () => {
    mocks.resolvePaymentReview.mockRejectedValue(new Error("Mất kết nối"))
    mocks.getPaymentReview.mockResolvedValueOnce(review()).mockRejectedValueOnce(new Error("Không thể đọc lại chi tiết"))
    renderPage()
    await openDetails()
    fillNote()
    fireEvent.click(screen.getByRole("radio", { name: "Không phải trả trùng" }))
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận xử lý" }))
    await screen.findByText("Không thể đọc lại chi tiết")
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
    await waitFor(() => expect(mocks.getPaymentReviews).toHaveBeenCalledTimes(2))
  })

  it("displays existing payment details and nullable timestamps without a form for resolved items", async () => {
    mocks.getPaymentReview.mockResolvedValue(review({ status: "Resolved", resolution: "NotDuplicate", transactionDateTime: null, existingPaidAtUtc: null }))
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: "Xem mục đối soát #12" }))
    const reference = await screen.findByText("PAYOS-123456")
    expect(reference).toBeTruthy()
    expect(screen.getByText("#3")).toBeTruthy()
    expect(screen.getByText("Cash")).toBeTruthy()
    expect(screen.getByText("Hoá đơn đã có khoản thanh toán")).toBeTruthy()
    expect(screen.getByText("Không phải trả trùng")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
  })

  it("paginates in groups of 20, resets on status change and supports reloading", async () => {
    mocks.getPaymentReviews.mockResolvedValueOnce(Array.from({ length: 20 }, (_, index) => review({ id: index + 1 })))
    renderPage()
    await screen.findByText("#20")
    fireEvent.click(screen.getByRole("button", { name: "Sau" }))
    await screen.findByText("#12")
    expect(mocks.getPaymentReviews).toHaveBeenLastCalledWith("admin-token", { status: "Open", page: 2, pageSize: 20 }, expect.any(AbortSignal))
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Resolved" } })
    await waitFor(() => expect(mocks.getPaymentReviews).toHaveBeenLastCalledWith("admin-token", { status: "Resolved", page: 1, pageSize: 20 }, expect.any(AbortSignal)))
    await screen.findByText("#12")
    expect(screen.getByText("Trang 1")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Tải lại danh sách" }))
    await waitFor(() => expect(mocks.getPaymentReviews).toHaveBeenCalledTimes(4))
  })

  it("aborts detail requests on filter change and ignores late responses", async () => {
    const detail = deferred<PaymentReviewDto>()
    mocks.getPaymentReview.mockReturnValue(detail.promise)
    const view = renderPage()
    fireEvent.click(await screen.findByRole("button", { name: "Xem mục đối soát #12" }))
    const signal = mocks.getPaymentReview.mock.calls[0][2] as AbortSignal
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Resolved" } })
    expect(signal.aborted).toBe(true)
    await act(async () => detail.resolve(review()))
    expect(screen.queryByText("PAYOS-123456")).toBeNull()
    view.unmount()
    const calls = mocks.getPaymentReviews.mock.calls
    expect((calls[calls.length - 1][2] as AbortSignal).aborted).toBe(true)
  })

  it("ignores a late list response for the previous filter", async () => {
    const oldList = deferred<PaymentReviewDto[]>()
    mocks.getPaymentReviews.mockReturnValueOnce(oldList.promise).mockResolvedValueOnce([review({ id: 20, status: "Resolved" })])
    renderPage()
    const signal = mocks.getPaymentReviews.mock.calls[0][2] as AbortSignal
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Resolved" } })
    await screen.findByText("#20")
    expect(signal.aborted).toBe(true)
    await act(async () => oldList.resolve([review()]))
    expect(screen.queryByText("#12")).toBeNull()
  })

  it("clears details and stops requests when admin access is lost", async () => {
    const view = renderPage()
    await openDetails()
    const signal = mocks.getPaymentReview.mock.calls[0][2] as AbortSignal
    mocks.auth.user.role = "MANAGER"
    view.rerender(<MemoryRouter><ManagementPaymentReviewsPage /></MemoryRouter>)
    expect(signal.aborted).toBe(true)
    expect(screen.queryByText("PAYOS-123456")).toBeNull()
    expect(screen.getByRole("alert").textContent).toContain("Bạn không có quyền")
    expect(mocks.getPaymentReviews).toHaveBeenCalledTimes(1)
  })

  it.each([
    [403, "Forbidden", "Bạn không có quyền thực hiện thao tác này."],
    [404, "Not found", "Không tìm thấy mục đối soát thanh toán."],
    [400, "Dữ liệu đối soát không hợp lệ", "Dữ liệu đối soát không hợp lệ"],
  ])("shows the appropriate message for HTTP %s", async (status, message, expected) => {
    mocks.getPaymentReview.mockRejectedValue(new ApiError(message, { status }))
    renderPage()
    fireEvent.click(await screen.findByRole("button", { name: "Xem mục đối soát #12" }))
    await screen.findByText(expected)
    expect(screen.queryByRole("button", { name: "Xác nhận xử lý" })).toBeNull()
  })
})

describe("paymentReviewService HTTP contract", () => {
  it("uses the API base with no-store for list, detail and a JSON resolve POST", async () => {
    const service = await vi.importActual<typeof import("@/services/paymentReviewService")>("@/services/paymentReviewService")
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(async () => new Response(JSON.stringify(review()), {
      status: 200, headers: { "Content-Type": "application/json" },
    }))
    vi.stubGlobal("fetch", fetchMock)
    const controller = new AbortController()
    await service.getPaymentReviews("admin-token", { status: "Resolved", page: 2, pageSize: 20 }, controller.signal)
    expect(fetchMock.mock.calls[0][0]).toBe("/api/payment-reviews?status=Resolved&page=2&pageSize=20")
    await service.getPaymentReview("admin-token", 12, controller.signal)
    expect(fetchMock.mock.calls[1][0]).toBe("/api/payment-reviews/12")
    const input = { resolution: "NotDuplicate" as const, note: "Đã kiểm tra", evidenceReference: null }
    await service.resolvePaymentReview("admin-token", 12, input)
    expect(fetchMock.mock.calls[2][0]).toBe("/api/payment-reviews/12/resolve")
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: "POST", body: JSON.stringify(input) })
    for (const [, options] of fetchMock.mock.calls) {
      expect(options?.cache).toBe("no-store")
      expect(new Headers(options?.headers).get("Authorization")).toBe("Bearer admin-token")
    }
    expect(fetchMock.mock.calls[0][1]?.signal).toBe(controller.signal)
    expect(fetchMock.mock.calls[1][1]?.signal).toBe(controller.signal)
    fetchMock.mockResolvedValueOnce(new Response("invalid JSON", { status: 200 }))
    await expect(service.getPaymentReview("admin-token", 12)).rejects.toBeInstanceOf(ApiError)
  })
})
