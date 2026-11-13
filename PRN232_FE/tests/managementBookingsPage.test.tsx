import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementBookingsPage from "@/pages/ManagementBookingsPage/ManagementBookingsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"

const mocks = vi.hoisted(() => ({
  getBookings: vi.fn(),
  cancelBooking: vi.fn(),
  getBookingRefunds: vi.fn(),
  createBookingRefund: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "manager-token", user: { id: "7", role: "MANAGER" } }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/bookingService", () => ({
  getBookings: mocks.getBookings,
  cancelBooking: mocks.cancelBooking,
}))
vi.mock("@/services/bookingRefundService", () => ({
  getBookingRefunds: mocks.getBookingRefunds,
  createBookingRefund: mocks.createBookingRefund,
}))

const inMinutes = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString()

const booking = (overrides: Partial<BookingDto> = {}): BookingDto => ({
  bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", depositAmount: 3_000_000, status: "Deposited",
  expiryDate: inMinutes(7 * 24 * 60), customerPhone: "0908123456", contractId: null,
  ...overrides,
})

function renderPage() {
  return render(<MemoryRouter><ManagementBookingsPage /></MemoryRouter>)
}

beforeEach(() => {
  mocks.getBookings.mockResolvedValue([
    booking(),
    booking({ bookingId: 8, roomCode: "A102", status: "Pending", expiryDate: inMinutes(10) }),
    booking({ bookingId: 7, roomCode: "A103", status: "Cancelled" }),
    booking({ bookingId: 6, roomCode: "A104", contractId: 3 }),
  ])
})

describe("ManagementBookingsPage", () => {
  it("lists scoped bookings and cancels a paid one only after the manager confirms", async () => {
    mocks.cancelBooking.mockResolvedValue(booking({ status: "Cancelled" }))
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    renderPage()

    await screen.findByText("#9")
    expect(mocks.getBookings).toHaveBeenCalledWith("manager-token", expect.any(AbortSignal))
    expect(screen.getByText("Đã đặt cọc")).toBeTruthy()
    expect(screen.getByText("Chờ thanh toán")).toBeTruthy()
    expect(screen.getByText("Đã lập hợp đồng")).toBeTruthy()
    expect(screen.getByRole("link", { name: "Tạo hợp đồng" }).getAttribute("href")).toBe("/management/contracts/new?bookingId=9")
    const cancelButtons = screen.getAllByRole("button", { name: "Huỷ booking" })
    expect(cancelButtons).toHaveLength(2)

    fireEvent.click(cancelButtons[0])
    expect(confirm.mock.calls[0][0]).toContain("đã đặt cọc phòng A101")
    expect(mocks.cancelBooking).not.toHaveBeenCalled()

    fireEvent.click(cancelButtons[0])
    await screen.findByText("Đã huỷ booking #9 và thông báo cho khách.")
    expect(mocks.cancelBooking).toHaveBeenCalledWith("manager-token", 9)
    expect(screen.getAllByText("Đã huỷ")).toHaveLength(2)
    expect(screen.getAllByRole("button", { name: "Huỷ booking" })).toHaveLength(1)
    expect(screen.queryByRole("link", { name: "Tạo hợp đồng" })).toBeNull()
  })

  it("shows the refund status read-only and hides contract and cancel actions while a refund is active", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({ refundStatus: "PendingReview", canRequestRefund: true }),
      booking({ bookingId: 5, roomCode: "A105", refundStatus: "Rejected" }),
    ])
    mocks.getBookingRefunds.mockResolvedValue([{
      bookingRefundRequestId: 4, bookingId: 9, originalPaymentId: 2, requestedAmount: 3_000_000, currency: "VND",
      status: "PendingReview", reason: "Đổi kế hoạch", recipientBankBin: "970436", maskedRecipientAccountNumber: "****6789",
      recipientAccountName: "NGUYEN AN", accountNameVerified: false, createdAtUtc: "2026-11-13T08:00:00Z",
      updatedAtUtc: "2026-11-13T08:00:00Z", approvedAmount: null, reviewNote: null, reviewedAtUtc: null, refundedAtUtc: null,
    }])
    renderPage()

    expect(await screen.findByText("Hoàn cọc: Chờ Admin xét duyệt")).toBeTruthy()
    expect(screen.getByText("Đang xử lý hoàn cọc, chưa thể lập hợp đồng.")).toBeTruthy()
    // Chỉ booking có đơn bị từ chối mới còn được lập hợp đồng hoặc huỷ.
    expect(screen.getAllByRole("link", { name: "Tạo hợp đồng" })).toHaveLength(1)
    expect(screen.getAllByRole("button", { name: "Huỷ booking" })).toHaveLength(1)
    expect(screen.getAllByText("Đến khi lập hợp đồng hoặc hoàn cọc")).toHaveLength(2)

    fireEvent.click(screen.getAllByRole("button", { name: "Xem hoàn cọc" })[0])
    expect(await screen.findByText("Yêu cầu #4")).toBeTruthy()
    expect(mocks.getBookingRefunds).toHaveBeenCalledWith("manager-token", 9, expect.any(AbortSignal))
    expect(screen.queryByRole("button", { name: "Gửi yêu cầu" })).toBeNull()
  })

  it("explains a conflict when the booking changed before cancelling", async () => {
    mocks.cancelBooking.mockRejectedValue(new ApiError("Booking is already cancelled.", { status: 409 }))
    vi.spyOn(window, "confirm").mockReturnValue(true)
    renderPage()

    fireEvent.click((await screen.findAllByRole("button", { name: "Huỷ booking" }))[0])

    await screen.findByText("Booking vừa đổi trạng thái, vui lòng tải lại trang.")
  })
})
