import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import BookingRefundModal from "@/components/customer/BookingRefundModal"
import type { BookingDto } from "@/types/booking"
import type { BookingRefundDto } from "@/types/bookingRefund"

const mocks = vi.hoisted(() => ({ list: vi.fn(), create: vi.fn() }))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => ({ accessToken: "customer-token" }) }))
vi.mock("@/services/bookingRefundService", () => ({ getBookingRefunds: mocks.list, createBookingRefund: mocks.create }))

const booking: BookingDto = {
  bookingId: 7, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise", customerId: 10,
  customerName: "Customer A", depositAmount: 3_000_000, status: "Deposited", expiryDate: "2026-11-20T00:00:00Z",
  customerPhone: "0900000000", contractId: null, refundStatus: null, canRequestRefund: true,
}

const refund = (changes: Partial<BookingRefundDto> = {}): BookingRefundDto => ({
  bookingRefundRequestId: 4, bookingId: 7, originalPaymentId: 9, requestedAmount: 3_000_000, currency: "VND",
  status: "PendingReview", reason: "Đổi kế hoạch", recipientBankBin: "970436", maskedRecipientAccountNumber: "****6789",
  recipientAccountName: "NGUYEN VAN A", accountNameVerified: false, createdAtUtc: "2026-11-13T08:00:00Z",
  updatedAtUtc: "2026-11-13T08:00:00Z", approvedAmount: null, reviewNote: null, reviewedAtUtc: null, refundedAtUtc: null,
  ...changes,
})

function fillForm() {
  fireEvent.change(screen.getByLabelText("Lý do"), { target: { value: "  Đổi kế hoạch  " } })
  fireEvent.change(screen.getByLabelText("Ngân hàng nhận"), { target: { value: "970436" } })
  fireEvent.change(screen.getByLabelText("Số tài khoản"), { target: { value: "0123 456 789" } })
  fireEvent.change(screen.getByLabelText("Tên chủ tài khoản"), { target: { value: "nguyen van a" } })
}

beforeEach(() => {
  mocks.list.mockReset().mockResolvedValue([])
  mocks.create.mockReset().mockResolvedValue(refund())
})

describe("BookingRefundModal", () => {
  it("validates, then sends the account number as text without dropping the leading zero", async () => {
    const onChanged = vi.fn()
    render(<BookingRefundModal booking={booking} onClose={vi.fn()} onChanged={onChanged} />)
    await screen.findByText("Bạn chưa gửi yêu cầu hoàn cọc nào cho booking này.")

    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }))
    expect(screen.getByRole("alert").textContent).toBe("Vui lòng nhập lý do xin hoàn cọc.")
    expect(mocks.create).not.toHaveBeenCalled()

    fillForm()
    mocks.list.mockResolvedValue([refund()])
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }))

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith("customer-token", 7, {
      reason: "Đổi kế hoạch", recipientBankBin: "970436", recipientAccountNumber: "0123456789", recipientAccountName: "NGUYEN VAN A",
    }))
    expect(await screen.findByText("Chờ Admin xét duyệt")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Gửi yêu cầu" })).toBeNull()
    expect(onChanged).toHaveBeenCalled()
  })

  it("only shows history while a request is still being processed", async () => {
    mocks.list.mockResolvedValue([refund({ status: "Approved", approvedAmount: 2_500_000, reviewNote: "Đã đối chiếu" })])
    render(<BookingRefundModal booking={{ ...booking, refundStatus: "Approved", canRequestRefund: false }} onClose={vi.fn()} onChanged={vi.fn()} />)
    expect(await screen.findByText("Đã duyệt, chờ chuyển tiền")).toBeTruthy()
    expect(screen.getByText("Ghi chú Admin:")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Gửi yêu cầu" })).toBeNull()
  })

  it("lets the customer resend after a rejection and shows the rejection reason", async () => {
    mocks.list.mockResolvedValue([refund({ status: "Rejected", reviewNote: "Sai số tài khoản" })])
    render(<BookingRefundModal booking={{ ...booking, refundStatus: "Rejected" }} onClose={vi.fn()} onChanged={vi.fn()} />)
    expect(await screen.findByText("Sai số tài khoản")).toBeTruthy()
    expect(screen.getByText("Gửi lại yêu cầu hoàn cọc")).toBeTruthy()
  })

  it("never resends automatically when the response is lost", async () => {
    mocks.create.mockRejectedValue(new ApiError("Không thể kết nối đến máy chủ. Vui lòng thử lại."))
    render(<BookingRefundModal booking={booking} onClose={vi.fn()} onChanged={vi.fn()} />)
    await screen.findByText("Bạn chưa gửi yêu cầu hoàn cọc nào cho booking này.")
    fillForm()
    fireEvent.click(screen.getByRole("button", { name: "Gửi yêu cầu" }))

    expect((await screen.findByRole("alert")).textContent).toMatch(/Cập nhật trạng thái/)
    const submit = screen.getByRole("button", { name: "Gửi yêu cầu" }) as HTMLButtonElement
    expect(submit.disabled).toBe(true)
    fireEvent.click(submit)
    expect(mocks.create).toHaveBeenCalledTimes(1)
  })
})
