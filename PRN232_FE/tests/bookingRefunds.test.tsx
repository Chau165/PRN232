import { act, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import BookingRefundPanel from "@/components/customer/BookingRefundPanel"
import ManagementRefundsPage from "@/pages/ManagementRefundsPage/ManagementRefundsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"
import type {
  BookingRefundDto,
  BookingRefundReviewDto,
} from "@/types/bookingRefund"

const mocks = vi.hoisted(() => ({
  auth: { accessToken: "token", user: { id: "10", role: "CUSTOMER" } },
  history: vi.fn(),
  create: vi.fn(),
  list: vi.fn(),
  detail: vi.fn(),
  review: vi.fn(),
}))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => mocks.auth }))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => (
    <main>{children}</main>
  ),
}))
vi.mock("@/services/bookingRefundService", () => ({
  getBookingRefunds: mocks.history,
  createBookingRefund: mocks.create,
  getRefundReviews: mocks.list,
  getRefundReview: mocks.detail,
  reviewBookingRefund: mocks.review,
}))
const booking: BookingDto = {
  bookingId: 7,
  roomId: 1,
  roomCode: "A101",
  propertyId: 1,
  propertyName: "Sunrise",
  customerId: 10,
  customerName: "An",
  depositAmount: 3000000,
  status: "Deposited",
  expiryDate: "2020-01-01T00:00:00Z",
  customerPhone: "0900",
  contractId: null,
  canRequestRefund: true,
  refundStatus: null,
}
const refund = (change: Partial<BookingRefundDto> = {}): BookingRefundDto => ({
  bookingRefundRequestId: 12,
  bookingId: 7,
  originalPaymentId: 3,
  requestedAmount: 3000000,
  approvedAmount: null,
  currency: "VND",
  status: "PendingReview",
  reason: "Đổi kế hoạch",
  recipientBankBin: "970422",
  maskedRecipientAccountNumber: "****7890",
  recipientAccountName: "NGUYEN AN",
  accountNameVerified: false,
  createdAtUtc: "2026-11-01T00:00:00Z",
  updatedAtUtc: "2026-11-01T00:00:00Z",
  reviewNote: null,
  reviewedAtUtc: null,
  refundedAtUtc: null,
  ...change,
})
const review = (
  change: Partial<BookingRefundReviewDto> = {},
): BookingRefundReviewDto => ({
  ...refund(),
  customerId: 10,
  depositAmount: 3000000,
  recipientAccountNumber: "001234567890",
  reviewedByAdminId: null,
  ...change,
})
const panel = (value = booking) => (
  <BookingRefundPanel booking={value} onChanged={vi.fn()} />
)
const adminPage = () =>
  render(
    <MemoryRouter>
      <ManagementRefundsPage />
    </MemoryRouter>,
  )
function fillCustomer() {
  fireEvent.change(screen.getByLabelText("Lý do xin hoàn"), {
    target: { value: " Đổi kế hoạch " },
  })
  fireEvent.change(screen.getByLabelText("Ngân hàng nhận"), {
    target: { value: "970422" },
  })
  fireEvent.change(screen.getByLabelText("Số tài khoản nhận"), {
    target: { value: "001234567890" },
  })
  fireEvent.change(screen.getByLabelText("Tên chủ tài khoản"), {
    target: { value: "NGUYEN AN" },
  })
}
beforeEach(() => {
  sessionStorage.clear()
  mocks.auth.user.role = "CUSTOMER"
  mocks.auth.accessToken = "token"
  mocks.history.mockResolvedValue([])
  mocks.create.mockResolvedValue(refund())
  mocks.list.mockResolvedValue([review()])
  mocks.detail.mockResolvedValue(review())
  mocks.review.mockResolvedValue(
    review({
      status: "Approved",
      approvedAmount: 2500000,
      reviewNote: "Đã đối chiếu",
      reviewedByAdminId: 9,
    }),
  )
})
afterEach(() => vi.useRealTimers())

describe("Customer refund form and tracking", () => {
  it("bounds automatic polling, pauses clearly, and lets the user refresh without another POST", async () => {
    vi.useFakeTimers(); mocks.history.mockResolvedValue([refund()]); render(panel())
    await act(async () => { await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(300000) })
    expect(mocks.history).toHaveBeenCalledTimes(20)
    expect(screen.getByText(/Đã tạm dừng cập nhật tự động/)).toBeTruthy()
    await act(async () => { await vi.advanceTimersByTimeAsync(300000) })
    expect(mocks.history).toHaveBeenCalledTimes(20); expect(mocks.create).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole("button", { name: "Cập nhật hoàn cọc" }))
    await act(async () => { await Promise.resolve() })
    expect(mocks.history).toHaveBeenCalledTimes(21)
  })
  it("sends only destination and reason, preserves leading zeroes, never caches bank data and prevents double submission", async () => {
    let finish: (value: BookingRefundDto) => void
    mocks.create.mockImplementation(
      () =>
        new Promise<BookingRefundDto>((resolve) => {
          finish = resolve
        }),
    )
    const storage = vi.spyOn(Storage.prototype, "setItem")
    render(panel())
    await screen.findByLabelText("Lý do xin hoàn")
    fillCustomer()
    const button = screen.getByRole("button", { name: "Gửi yêu cầu hoàn cọc" })
    fireEvent.click(button)
    fireEvent.click(button)
    expect(mocks.create).toHaveBeenCalledTimes(1)
    expect(mocks.create).toHaveBeenCalledWith("token", 7, {
      reason: "Đổi kế hoạch",
      recipientBankBin: "970422",
      recipientAccountNumber: "001234567890",
      recipientAccountName: "NGUYEN AN",
    })
    expect(screen.queryByRole("spinbutton")).toBeNull()
    expect(storage).not.toHaveBeenCalled()
    mocks.history.mockResolvedValue([refund()])
    await act(async () => finish(refund()))
    await screen.findByText("Chờ Admin xét duyệt")
    expect(
      screen.queryByRole("button", { name: "Gửi yêu cầu hoàn cọc" }),
    ).toBeNull()
  })
  it("validates account without converting it to a number", async () => {
    render(panel())
    await screen.findByLabelText("Lý do xin hoàn")
    fillCustomer()
    fireEvent.change(screen.getByLabelText("Số tài khoản nhận"), {
      target: { value: "12 34" },
    })
    fireEvent.submit(
      screen
        .getByRole("button", { name: "Gửi yêu cầu hoàn cọc" })
        .closest("form")!,
    )
    expect((await screen.findByRole("alert")).textContent).toContain(
      "không có khoảng trắng",
    )
    expect(mocks.create).not.toHaveBeenCalled()
  })
  it("requires a read after a lost submission response instead of blindly posting another request", async () => {
    mocks.create.mockRejectedValue(new ApiError("connection lost"))
    render(panel())
    await screen.findByLabelText("Lý do xin hoàn")
    fillCustomer()
    fireEvent.click(
      screen.getByRole("button", { name: "Gửi yêu cầu hoàn cọc" }),
    )
    await screen.findByText(
      "Bấm cập nhật hoàn cọc để kiểm tra kết quả trước khi gửi tiếp.",
    )
    expect(
      (screen.getByRole("button", {
        name: "Gửi yêu cầu hoàn cọc",
      }) as HTMLButtonElement).disabled,
    ).toBe(true)
    mocks.history.mockResolvedValue([refund()])
    fireEvent.click(screen.getByRole("button", { name: "Cập nhật hoàn cọc" }))
    await screen.findByText("Chờ Admin xét duyệt")
    expect(mocks.create).toHaveBeenCalledTimes(1)
  })
  it("allows resubmission after rejection and shows the admin reason", async () => {
    mocks.history.mockResolvedValue([
      refund({ status: "Rejected", reviewNote: "Kiểm tra lại tài khoản" }),
    ])
    render(panel())
    await screen.findByText("Ý kiến Admin: Kiểm tra lại tài khoản")
    expect(
      screen.getByRole("button", { name: "Gửi yêu cầu hoàn cọc" }),
    ).toBeTruthy()
  })
  it("keeps Manager tracking read-only and masked", async () => {
    mocks.auth.user.role = "MANAGER"
    mocks.history.mockResolvedValue([
      refund({ status: "Approved", approvedAmount: 2500000 }),
    ])
    render(panel())
    await screen.findByText("Đã duyệt, chờ chuyển tiền", { selector: "span" })
    expect(
      screen.queryByRole("button", { name: "Gửi yêu cầu hoàn cọc" }),
    ).toBeNull()
    expect(screen.queryByText(/001234567890/)).toBeNull()
    expect(screen.getByText(/\*\*\*\*7890/)).toBeTruthy()
  })
  it("does not offer an online request when server eligibility is false", async () => {
    render(panel({ ...booking, canRequestRefund: false }))
    await screen.findByText(/chưa đủ điều kiện/)
    expect(
      screen.queryByRole("button", { name: "Gửi yêu cầu hoàn cọc" }),
    ).toBeNull()
  })
  it("clears a prior account's history when token or booking changes and ignores a late response", async () => {
    let finish: (value: BookingRefundDto[]) => void
    mocks.history
      .mockImplementationOnce(
        () =>
          new Promise<BookingRefundDto[]>((resolve) => {
            finish = resolve
          }),
      )
      .mockResolvedValue([])
    const view = render(panel())
    mocks.auth.accessToken = "other-token"
    view.rerender(panel({ ...booking, bookingId: 8 }))
    await screen.findByText("Chưa có yêu cầu hoàn cọc.")
    await act(async () => finish([refund({ status: "Refunded" })]))
    expect(screen.queryByText("Đã hoàn cọc")).toBeNull()
  })
})

describe("Admin refund review", () => {
  beforeEach(() => {
    mocks.auth.user.role = "ADMIN"
  })
  async function open() {
    adminPage()
    fireEvent.click(await screen.findByRole("button", { name: "Xem đơn #12" }))
    await screen.findByText("001234567890")
  }
  it("displays source deposit and destination, requires review confirmation, and submits one explicit approved amount", async () => {
    await open()
    expect(
      screen.getByText(/chưa được ngân hàng xác minh tự động/),
    ).toBeTruthy()
    const approve = screen.getByRole("button", { name: "Duyệt hoàn cọc" })
    expect((approve as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText("Số tiền duyệt hoàn (VND)"), {
      target: { value: "2500000" },
    })
    fireEvent.change(screen.getByLabelText("Lý do xét duyệt"), {
      target: { value: "Đã đối chiếu" },
    })
    fireEvent.click(screen.getByRole("checkbox"))
    mocks.detail.mockResolvedValue(
      review({
        status: "Approved",
        approvedAmount: 2500000,
        reviewNote: "Đã đối chiếu",
      }),
    )
    fireEvent.click(approve)
    fireEvent.click(approve)
    await screen.findByText("Đã duyệt, chờ chuyển tiền")
    expect(mocks.review).toHaveBeenCalledTimes(1)
    expect(mocks.review).toHaveBeenCalledWith("token", 12, {
      approve: true,
      approvedAmount: 2500000,
      reviewNote: "Đã đối chiếu",
    })
    expect(screen.queryByRole("button", { name: "Duyệt hoàn cọc" })).toBeNull()
  })
  it("requires a rejection reason but not an approval checkbox or amount", async () => {
    await open()
    fireEvent.click(screen.getByRole("button", { name: "Từ chối yêu cầu" }))
    await screen.findByText(/Nhập lý do xét duyệt/)
    expect(mocks.review).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText("Lý do xét duyệt"), {
      target: { value: "Tài khoản chưa khớp" },
    })
    mocks.review.mockResolvedValue(
      review({ status: "Rejected", reviewNote: "Tài khoản chưa khớp" }),
    )
    mocks.detail.mockResolvedValue(review({ status: "Rejected" }))
    fireEvent.click(screen.getByRole("button", { name: "Từ chối yêu cầu" }))
    await waitFor(() =>
      expect(mocks.review).toHaveBeenCalledWith("token", 12, {
        approve: false,
        approvedAmount: null,
        reviewNote: "Tài khoản chưa khớp",
      }),
    )
  })
  it("does not submit an amount above the received deposit", async () => {
    await open()
    fireEvent.change(screen.getByLabelText("Lý do xét duyệt"), {
      target: { value: "Đã kiểm tra" },
    })
    fireEvent.click(screen.getByRole("checkbox"))
    fireEvent.change(screen.getByLabelText("Số tiền duyệt hoàn (VND)"), {
      target: { value: "3000001" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Duyệt hoàn cọc" }))
    await screen.findByText(
      "Tiền hoàn phải là số nguyên dương, không vượt khoản yêu cầu.",
    )
    expect(mocks.review).not.toHaveBeenCalled()
  })
  it("refreshes a conflicting decision and does not overwrite another admin's approval", async () => {
    await open()
    fireEvent.change(screen.getByLabelText("Lý do xét duyệt"), {
      target: { value: "Từ chối" },
    })
    mocks.review.mockRejectedValue(
      new ApiError("Already reviewed", { status: 409 }),
    )
    mocks.detail.mockResolvedValue(review({ status: "Approved" }))
    fireEvent.click(screen.getByRole("button", { name: "Từ chối yêu cầu" }))
    await screen.findByText(/Booking hoặc đơn hoàn đã thay đổi/)
    await screen.findByText("Đã duyệt, chờ chuyển tiền")
    expect(screen.queryByRole("button", { name: "Từ chối yêu cầu" })).toBeNull()
    expect(mocks.review).toHaveBeenCalledTimes(1)
  })
  it("does not load admin financial details for Manager", () => {
    mocks.auth.user.role = "MANAGER"
    adminPage()
    expect(screen.getByRole("alert").textContent).toContain("Chỉ Admin")
    expect(mocks.list).not.toHaveBeenCalled()
    expect(mocks.detail).not.toHaveBeenCalled()
  })
})
