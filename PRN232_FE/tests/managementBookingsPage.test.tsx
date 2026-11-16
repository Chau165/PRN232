import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementBookingsPage from "@/pages/ManagementBookingsPage/ManagementBookingsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"

const mocks = vi.hoisted(() => ({
  getBookings: vi.fn(),
  cancelBooking: vi.fn(),
  markBookingDepositReturned: vi.fn(),
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
  markBookingDepositReturned: mocks.markBookingDepositReturned,
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
  it("cancels an unpaid booking and hides online refund tracking when refundStatus is null (C10)", async () => {
    mocks.cancelBooking.mockResolvedValue(booking({ bookingId: 8, status: "Cancelled" }))
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    renderPage()

    await screen.findByText("#9")
    expect(mocks.getBookings).toHaveBeenCalledWith("manager-token", expect.any(AbortSignal))
    expect(screen.getByText("Đã đặt cọc")).toBeTruthy()
    expect(screen.getByText("Chờ thanh toán")).toBeTruthy()
    expect(screen.getByText("Đã lập hợp đồng")).toBeTruthy()
    expect(screen.getByRole("link", { name: "Tạo hợp đồng" }).getAttribute("href")).toBe("/management/contracts/new?bookingId=9")
    const cancelButtons = screen.getAllByRole("button", { name: "Huỷ booking" })
    expect(cancelButtons).toHaveLength(1)
    expect(screen.queryByRole("button", { name: "Theo dõi hoàn cọc #9" })).toBeNull()
    expect(screen.getByRole("button", { name: "Hủy (hoàn 100% cọc) #9" })).toBeTruthy()

    fireEvent.click(cancelButtons[0])
    expect(confirm.mock.calls[0][0]).toContain("booking #8")
    expect(mocks.cancelBooking).not.toHaveBeenCalled()

    fireEvent.click(cancelButtons[0])
    await screen.findByText("Đã huỷ booking #8 và thông báo cho khách.")
    expect(mocks.cancelBooking).toHaveBeenCalledWith("manager-token", 8)
    expect(screen.getAllByText("Đã huỷ")).toHaveLength(2)
    expect(screen.queryByRole("button", { name: "Huỷ booking" })).toBeNull()
    expect(screen.getByRole("link", { name: "Tạo hợp đồng" })).toBeTruthy()
  })

  it("does not offer contract creation when a legacy refund is active, but still shows legacy refund tracking", async () => {
    mocks.getBookings.mockResolvedValue([booking({ refundStatus: "Approved" })])
    renderPage()
    await screen.findByText("Đã duyệt, chờ chuyển tiền")
    expect(screen.queryByRole("link", { name: "Tạo hợp đồng" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Huỷ booking" })).toBeNull()
    expect(screen.getByRole("button", { name: "Theo dõi hoàn cọc #9" })).toBeTruthy()
  })

  it("shows separate buttons for cancelling within hold (100% refund) vs releasing overdue room (customer loses deposit) and formats holdUntilUtc (C10)", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({ bookingId: 9, roomCode: "A101", holdUntilUtc: "2026-11-10T17:00:00Z", canReleaseRoom: true }),
      booking({ bookingId: 5, roomCode: "A105", holdUntilUtc: "2026-11-23T17:00:00Z", canReleaseRoom: false }),
      booking({
        bookingId: 4,
        roomCode: "A106",
        status: "Cancelled",
        cancellationReason: "LandlordCancelled",
        depositRefundDue: true,
      }),
    ])
    mocks.cancelBooking.mockImplementation(async (_token: string, id: number) =>
      booking({
        bookingId: id,
        status: "Cancelled",
        holdUntilUtc: null,
        canReleaseRoom: false,
        cancellationReason: id === 5 ? "LandlordCancelled" : "NoShow",
        depositRefundDue: id === 5,
      }),
    )
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true).mockReturnValueOnce(true)
    renderPage()

    await screen.findByText("#5")
    expect(screen.getByText("Giữ phòng đến hết 23/11/2026")).toBeTruthy()
    expect(screen.getByText("Giữ phòng đến hết 10/11/2026")).toBeTruthy()
    expect(screen.getAllByText("Quá hạn nhận phòng")).toHaveLength(1)
    expect(screen.getByText(/Lý do hủy: Quản lý \/ chủ trọ hủy/)).toBeTruthy()
    expect(screen.getByText(/Chờ hoàn cọc/)).toBeTruthy()

    // Booking #5: canReleaseRoom = false -> Hủy (hoàn 100% cọc)
    expect(screen.queryByRole("button", { name: "Nhả phòng (khách mất cọc) #5" })).toBeNull()
    const cancelFullRefundBtn = screen.getByRole("button", { name: "Hủy (hoàn 100% cọc) #5" })

    // Booking #9: canReleaseRoom = true -> Nhả phòng (khách mất cọc)
    expect(screen.queryByRole("button", { name: "Hủy (hoàn 100% cọc) #9" })).toBeNull()
    const releaseBtn = screen.getByRole("button", { name: "Nhả phòng (khách mất cọc) #9" })

    fireEvent.click(releaseBtn)
    expect(confirm.mock.calls[0][0]).toContain("tiền cọc không được hoàn")
    expect(mocks.cancelBooking).not.toHaveBeenCalled()

    fireEvent.click(releaseBtn)
    await screen.findByText("Đã giải phóng Phòng A101 (booking #9) và thông báo cho khách.")
    expect(mocks.cancelBooking).toHaveBeenCalledWith("manager-token", 9)
    expect(screen.queryByRole("button", { name: "Nhả phòng (khách mất cọc) #9" })).toBeNull()

    fireEvent.click(cancelFullRefundBtn)
    expect(confirm.mock.calls[2][0]).toContain("Khách sẽ được hoàn")
    expect(confirm.mock.calls[2][0]).toContain("Bạn cần tự chuyển tiền cho khách.")
    expect(mocks.cancelBooking).toHaveBeenCalledWith("manager-token", 5)
  })

  it("opens the C11 confirmation modal for 'Đã hoàn cọc', allows cancelling, and records deposit return", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({
        bookingId: 4,
        roomCode: "A106",
        customerName: "Nguyễn An",
        customerPhone: "0908123456",
        depositAmount: 3_000_000,
        status: "Cancelled",
        cancellationReason: "LandlordCancelled",
        depositRefundDue: true,
        depositReturnedAtUtc: null,
        depositReturnedByName: null,
      }),
    ])
    mocks.markBookingDepositReturned.mockResolvedValue(
      booking({
        bookingId: 4,
        roomCode: "A106",
        customerName: "Nguyễn An",
        customerPhone: "0908123456",
        depositAmount: 3_000_000,
        status: "Cancelled",
        cancellationReason: "LandlordCancelled",
        depositRefundDue: false,
        depositReturnedAtUtc: "2026-11-16T03:00:00Z",
        depositReturnedByName: "Quản lý Bình",
      }),
    )

    renderPage()
    await screen.findByText("#4")
    expect(screen.getByText(/Chờ hoàn cọc/)).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Đã hoàn cọc" }))
    const dialog = screen.getByRole("dialog", { name: "Xác nhận đã hoàn cọc" })
    expect(dialog.textContent).toContain("Khách Nguyễn An (0908123456) · Booking #4 · Phòng A106")
    expect(dialog.textContent).toContain("3.000.000")
    expect(dialog.textContent).toContain(
      "Chỉ xác nhận sau khi bạn ĐÃ chuyển tiền cho khách. Hệ thống không chuyển tiền.",
    )

    // Bấm Hủy -> đóng cửa sổ, không gọi API
    fireEvent.click(screen.getByRole("button", { name: "Hủy" }))
    expect(screen.queryByRole("dialog", { name: "Xác nhận đã hoàn cọc" })).toBeNull()
    expect(mocks.markBookingDepositReturned).not.toHaveBeenCalled()

    // Mở lại và bấm Xác nhận đã hoàn -> gọi API và cập nhật nhãn
    fireEvent.click(screen.getByRole("button", { name: "Đã hoàn cọc" }))
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận đã hoàn" }))

    await screen.findByText(/Đã hoàn cọc lúc .* — Quản lý Bình/)
    expect(mocks.markBookingDepositReturned).toHaveBeenCalledWith("manager-token", 4)
    expect(screen.queryByRole("button", { name: "Đã hoàn cọc" })).toBeNull()
  })

  it("explains a conflict when the booking changed before cancelling", async () => {
    mocks.cancelBooking.mockRejectedValue(new ApiError("Booking is already cancelled.", { status: 409 }))
    vi.spyOn(window, "confirm").mockReturnValue(true)
    renderPage()

    fireEvent.click((await screen.findAllByRole("button", { name: "Huỷ booking" }))[0])

    await screen.findByText("Booking vừa đổi trạng thái, vui lòng tải lại trang.")
  })
})
