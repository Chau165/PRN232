import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import ManagementBookingsPage from "@/pages/ManagementBookingsPage/ManagementBookingsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/api/bookingsApi"

const mocks = vi.hoisted(() => ({
  getBookings: vi.fn(),
  cancelBooking: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "manager-token", user: { id: "7", role: "MANAGER" } }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/api/bookingsApi", () => ({
  getBookings: mocks.getBookings,
  cancelBooking: mocks.cancelBooking,
}))

const inMinutes = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString()

const booking = (overrides: Partial<BookingDto> = {}): BookingDto => ({
  bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", depositAmount: 3_000_000, status: "Deposited",
  expiryDate: inMinutes(7 * 24 * 60),
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
  })

  it("explains a conflict when the booking changed before cancelling", async () => {
    mocks.cancelBooking.mockRejectedValue(new ApiError("Booking is already cancelled.", { status: 409 }))
    vi.spyOn(window, "confirm").mockReturnValue(true)
    renderPage()

    fireEvent.click((await screen.findAllByRole("button", { name: "Huỷ booking" }))[0])

    await screen.findByText("Booking vừa đổi trạng thái, vui lòng tải lại trang.")
  })
})
