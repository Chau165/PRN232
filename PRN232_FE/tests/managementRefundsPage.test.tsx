import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import ManagementRefundsPage from "@/pages/ManagementRefundsPage/ManagementRefundsPage"
import type { BookingRefundReviewDto } from "@/types/bookingRefund"

const mocks = vi.hoisted(() => ({
  getRefundReviews: vi.fn(),
  getRefundReview: vi.fn(),
  reviewRefund: vi.fn(),
  auth: { accessToken: "admin-token", user: { id: "7", role: "ADMIN" } },
}))

vi.mock("@/auth/AuthContext", () => ({ useAuth: () => mocks.auth }))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/bookingRefundService", () => ({
  getRefundReviews: mocks.getRefundReviews,
  getRefundReview: mocks.getRefundReview,
  reviewRefund: mocks.reviewRefund,
}))

function refund(overrides: Partial<BookingRefundReviewDto> = {}): BookingRefundReviewDto {
  return {
    bookingRefundRequestId: 12, bookingId: 9, customerId: 10, originalPaymentId: 3,
    depositAmount: 3_000_000, requestedAmount: 2_000_000, approvedAmount: null,
    currency: "VND", status: "PendingReview", reason: "Không thể chuyển đến",
    recipientBankBin: "970436", recipientAccountNumber: "0012345678",
    recipientAccountName: "NGUYEN AN", accountNameVerified: false,
    reviewedByAdminId: null, reviewNote: null, createdAtUtc: "2026-11-13T03:00:00Z",
    updatedAtUtc: "2026-11-13T03:00:00Z", reviewedAtUtc: null, refundedAtUtc: null,
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
  return render(<MemoryRouter><ManagementRefundsPage /></MemoryRouter>)
}

async function openDetails() {
  fireEvent.click(await screen.findByRole("button", { name: "Xem yêu cầu #12" }))
  await screen.findByLabelText("Số tiền duyệt (VND)")
}

function fillNote() {
  fireEvent.change(screen.getByLabelText("Ghi chú xét duyệt (bắt buộc)"), { target: { value: "Đã đối chiếu chứng từ" } })
}

beforeEach(() => {
  vi.resetAllMocks()
  mocks.auth = { accessToken: "admin-token", user: { id: "7", role: "ADMIN" } }
  mocks.getRefundReviews.mockResolvedValue([refund()])
  mocks.getRefundReview.mockResolvedValue(refund())
  mocks.reviewRefund.mockResolvedValue(refund({ status: "Approved", approvedAmount: 1_500_000 }))
})

describe("ManagementRefundsPage", () => {
  it("loads PendingReview by default and masks accounts in the list", async () => {
    renderPage()
    await screen.findByText("#12")
    expect(mocks.getRefundReviews).toHaveBeenCalledWith("admin-token", { status: "PendingReview", page: 1, pageSize: 20 }, expect.any(AbortSignal))
    expect(screen.getByRole("combobox").getAttribute("disabled")).toBeNull()
    expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe("PendingReview")
    expect(screen.getByText("****5678")).toBeTruthy()
    expect(screen.queryByText("0012345678")).toBeNull()
    expect(screen.getByText("Vietcombank")).toBeTruthy()
    expect((screen.getByRole("button", { name: "Sau" }) as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole("button", { name: "Trước" }) as HTMLButtonElement).disabled).toBe(true)
  })

  it("shows full detail and approves only with a valid amount and confirmation", async () => {
    renderPage()
    await openDetails()
    expect(mocks.getRefundReview).toHaveBeenCalledWith("admin-token", 12, expect.any(AbortSignal))
    expect(screen.getByText("0012345678")).toBeTruthy()
    expect(screen.getByText("Tên do khách cung cấp, chưa được ngân hàng xác minh")).toBeTruthy()
    expect((screen.getByLabelText("Số tiền duyệt (VND)") as HTMLInputElement).value).toBe("2000000")
    fillNote()
    fireEvent.click(screen.getByRole("button", { name: "Duyệt" }))
    expect(mocks.reviewRefund).not.toHaveBeenCalled()
    expect(screen.getByRole("alert").textContent).toContain("xác nhận đã đối chiếu")
    fireEvent.click(screen.getByRole("checkbox"))
    for (const invalid of ["2000001", "0", "-1", "1.5"]) {
      fireEvent.change(screen.getByLabelText("Số tiền duyệt (VND)"), { target: { value: invalid } })
      fireEvent.click(screen.getByRole("button", { name: "Duyệt" }))
      expect(mocks.reviewRefund).not.toHaveBeenCalled()
    }
    fireEvent.change(screen.getByLabelText("Số tiền duyệt (VND)"), { target: { value: "1500000" } })
    fireEvent.click(screen.getByRole("button", { name: "Duyệt" }))
    await screen.findByText("Đã duyệt yêu cầu #12.")
    expect(mocks.reviewRefund).toHaveBeenCalledExactlyOnceWith("admin-token", 12, {
      approve: true, approvedAmount: 1_500_000, reviewNote: "Đã đối chiếu chứng từ",
    })
    expect(screen.queryByRole("button", { name: "Duyệt" })).toBeNull()
    await waitFor(() => expect(mocks.getRefundReviews).toHaveBeenCalledTimes(2))
  })

  it("rejects with approvedAmount null without a checkbox or valid approval amount", async () => {
    mocks.reviewRefund.mockResolvedValue(refund({ status: "Rejected" }))
    renderPage()
    await openDetails()
    fillNote()
    fireEvent.change(screen.getByLabelText("Số tiền duyệt (VND)"), { target: { value: "" } })
    fireEvent.click(screen.getByRole("button", { name: "Từ chối" }))
    await screen.findByText("Đã từ chối yêu cầu #12.")
    expect(mocks.reviewRefund).toHaveBeenCalledExactlyOnceWith("admin-token", 12, {
      approve: false, approvedAmount: null, reviewNote: "Đã đối chiếu chứng từ",
    })
  })

  it("requires a nonblank note of at most 2000 characters for either decision", async () => {
    renderPage()
    await openDetails()
    fireEvent.click(screen.getByRole("checkbox"))
    for (const note of ["", "   ", "a".repeat(2001)]) {
      fireEvent.change(screen.getByLabelText("Ghi chú xét duyệt (bắt buộc)"), { target: { value: note } })
      fireEvent.click(screen.getByRole("button", { name: "Duyệt" }))
      fireEvent.click(screen.getByRole("button", { name: "Từ chối" }))
      expect(mocks.reviewRefund).not.toHaveBeenCalled()
    }
  })

  it("locks decisions during POST and rereads detail and list after a 409", async () => {
    const post = deferred<BookingRefundReviewDto>()
    const reread = deferred<BookingRefundReviewDto>()
    mocks.reviewRefund.mockReturnValue(post.promise)
    mocks.getRefundReview.mockResolvedValueOnce(refund()).mockReturnValueOnce(reread.promise)
    renderPage()
    await openDetails()
    fillNote()
    const rejectButton = screen.getByRole("button", { name: "Từ chối" })
    fireEvent.click(rejectButton)
    fireEvent.click(rejectButton)
    expect(mocks.reviewRefund).toHaveBeenCalledTimes(1)
    expect((rejectButton as HTMLButtonElement).disabled || !!rejectButton.closest("fieldset[disabled]")).toBe(true)
    await act(async () => post.reject(new ApiError("Conflict", { status: 409 })))
    expect(mocks.getRefundReview).toHaveBeenCalledTimes(2)
    expect(screen.getByRole("alert").textContent).toContain("vừa thay đổi")
    expect(screen.queryByRole("button", { name: "Từ chối" })).toBeNull()
    await act(async () => reread.resolve(refund({ status: "Approved" })))
    await waitFor(() => expect(mocks.getRefundReviews).toHaveBeenCalledTimes(2))
    expect(screen.queryByRole("button", { name: "Duyệt" })).toBeNull()
  })

  it("keeps decisions hidden when the reread after a failed POST also fails", async () => {
    mocks.reviewRefund.mockRejectedValue(new Error("Mất kết nối"))
    mocks.getRefundReview.mockResolvedValueOnce(refund()).mockRejectedValueOnce(new Error("Không đọc được chi tiết"))
    renderPage()
    await openDetails()
    fillNote()
    fireEvent.click(screen.getByRole("button", { name: "Từ chối" }))
    await screen.findByText("Không đọc được chi tiết")
    expect(screen.queryByRole("button", { name: "Duyệt" })).toBeNull()
    await waitFor(() => expect(mocks.getRefundReviews).toHaveBeenCalledTimes(2))
  })

  it("paginates in groups of 20 and resets to page 1 for all statuses", async () => {
    mocks.getRefundReviews.mockResolvedValueOnce(Array.from({ length: 20 }, (_, index) => refund({ bookingRefundRequestId: index + 1 })))
    renderPage()
    await screen.findByText("#20")
    fireEvent.click(screen.getByRole("button", { name: "Sau" }))
    await screen.findByText("Trang 2")
    await screen.findByText("#12")
    expect(mocks.getRefundReviews).toHaveBeenLastCalledWith("admin-token", { status: "PendingReview", page: 2, pageSize: 20 }, expect.any(AbortSignal))
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "" } })
    await waitFor(() => expect(mocks.getRefundReviews).toHaveBeenLastCalledWith("admin-token", { status: undefined, page: 1, pageSize: 20 }, expect.any(AbortSignal)))
    expect(screen.getByText("Trang 1")).toBeTruthy()
  })

  it("aborts old list/detail requests on filter changes and ignores late results", async () => {
    const oldDetail = deferred<BookingRefundReviewDto>()
    mocks.getRefundReview.mockReturnValue(oldDetail.promise)
    const view = renderPage()
    fireEvent.click(await screen.findByRole("button", { name: "Xem yêu cầu #12" }))
    const detailSignal = mocks.getRefundReview.mock.calls[0][2] as AbortSignal
    const listSignal = mocks.getRefundReviews.mock.calls[0][2] as AbortSignal
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Rejected" } })
    expect(detailSignal.aborted).toBe(true)
    expect(listSignal.aborted).toBe(true)
    await act(async () => oldDetail.resolve(refund()))
    expect(screen.queryByText("0012345678")).toBeNull()
    view.unmount()
    const calls = mocks.getRefundReviews.mock.calls
    expect((calls[calls.length - 1][2] as AbortSignal).aborted).toBe(true)
  })

  it("never replaces a recorded decision with a late polling response", async () => {
    vi.useFakeTimers()
    try {
      await act(async () => { renderPage() })
      await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Xem yêu cầu #12" })) })
      const oldPoll = deferred<BookingRefundReviewDto>()
      mocks.getRefundReview.mockReturnValueOnce(oldPoll.promise)
      await act(async () => { vi.advanceTimersByTime(15_000) })
      const pollSignal = mocks.getRefundReview.mock.calls[1][2] as AbortSignal
      fillNote()
      fireEvent.click(screen.getByRole("checkbox"))
      await act(async () => fireEvent.click(screen.getByRole("button", { name: "Duyệt" })))
      expect(pollSignal.aborted).toBe(true)
      await act(async () => oldPoll.resolve(refund()))
      const detail = screen.getByRole("region", { name: "Chi tiết yêu cầu hoàn cọc" })
      expect(within(detail).getByText("Đã duyệt, chờ chuyển tiền")).toBeTruthy()
      expect(screen.queryByRole("button", { name: "Duyệt" })).toBeNull()
    } finally {
      vi.useRealTimers()
    }
  })

  it("clears account details and aborts requests when the signed-in account changes", async () => {
    const view = renderPage()
    await openDetails()
    const signal = mocks.getRefundReview.mock.calls[0][2] as AbortSignal
    mocks.auth = { accessToken: "other-token", user: { id: "8", role: "ADMIN" } }
    view.rerender(<MemoryRouter><ManagementRefundsPage /></MemoryRouter>)
    expect(signal.aborted).toBe(true)
    expect(screen.queryByText("0012345678")).toBeNull()
    await waitFor(() => expect(mocks.getRefundReviews).toHaveBeenLastCalledWith("other-token", { status: "PendingReview", page: 1, pageSize: 20 }, expect.any(AbortSignal)))
  })

  it("ignores a late response for the previous status filter", async () => {
    const oldList = deferred<BookingRefundReviewDto[]>()
    mocks.getRefundReviews.mockReturnValueOnce(oldList.promise).mockResolvedValueOnce([refund({ bookingRefundRequestId: 20, status: "Rejected" })])
    renderPage()
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Rejected" } })
    await screen.findByText("#20")
    await act(async () => oldList.resolve([refund()]))
    expect(screen.queryByText("#12")).toBeNull()
    expect(screen.getByText("#20")).toBeTruthy()
  })

  it("stops polling after 20 reads and still allows a manual refresh", async () => {
    vi.useFakeTimers()
    try {
      const view = await act(async () => renderPage())
      await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Xem yêu cầu #12" })) })
      for (let poll = 0; poll < 20; poll += 1) {
        await act(async () => { vi.advanceTimersByTime(15_000) })
      }
      expect(mocks.getRefundReview).toHaveBeenCalledTimes(21)
      expect(screen.getByText(/Đã tạm dừng cập nhật tự động/)).toBeTruthy()
      await act(async () => { vi.advanceTimersByTime(60_000) })
      expect(mocks.getRefundReview).toHaveBeenCalledTimes(21)
      await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Cập nhật chi tiết" })) })
      expect(mocks.getRefundReview).toHaveBeenCalledTimes(22)
      view.unmount()
      expect(vi.getTimerCount()).toBe(0)
    } finally {
      vi.useRealTimers()
    }
  })
})
