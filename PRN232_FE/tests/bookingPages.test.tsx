import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import DepositPage from "@/pages/DepositPage/DepositPage"
import MyBookingsPage from "@/pages/MyBookingsPage/MyBookingsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"
import { PublicApiError } from "@/services/publicBrowsingService"
import type { PublicRoomDetail } from "@/types/publicBrowsing"

const mocks = vi.hoisted(() => ({
  getBookings: vi.fn(),
  getBooking: vi.fn(),
  createBooking: vi.fn(),
  payBooking: vi.fn(),
  createLink: vi.fn(),
  openCheckout: vi.fn(),
  cancelBooking: vi.fn(),
  getRoom: vi.fn(),
  getViewingSchedules: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({
    accessToken: "customer-token",
    user: { id: "10", name: "Customer A", email: "customer@example.test", phone: "0900000000", role: "CUSTOMER" },
  }),
  useOptionalAuth: () => null,
}))
vi.mock("@/auth/CustomerNotificationsContext", () => ({
  useCustomerNotificationsState: () => ({
    notifications: [], unreadCount: 0, loading: false, error: null, refresh: vi.fn(), markRead: vi.fn(),
  }),
}))
vi.mock("@/services/bookingService", () => ({
  createBookingPaymentLink: mocks.createLink,
  getBookings: mocks.getBookings,
  getBooking: mocks.getBooking,
  createBooking: mocks.createBooking,
  payBooking: mocks.payBooking,
  cancelBooking: mocks.cancelBooking,
}))
vi.mock("@/utils/invoicePayment", async original => ({ ...await original<typeof import("@/utils/invoicePayment")>(), openPayOSCheckout: mocks.openCheckout }))
vi.mock("@/services/viewingScheduleService", () => ({ getViewingSchedules: mocks.getViewingSchedules }))
vi.mock("@/services/publicBrowsingService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/services/publicBrowsingService")>()),
  publicApi: { getRoom: mocks.getRoom },
}))

const inMinutes = (minutes: number) => new Date(Date.now() + minutes * 60_000).toISOString()

const booking = (overrides: Partial<BookingDto> = {}): BookingDto => ({
  bookingId: 7, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Customer A", depositAmount: 3_000_000, status: "Pending",
  expiryDate: inMinutes(15), customerPhone: "0900000000", contractId: null,
  ...overrides,
})

const room: PublicRoomDetail = {
  roomId: 501, roomCode: "A101", areaSqm: 20, roomType: { roomTypeId: 1, roomTypeName: "Studio" },
  baseRent: 3_000_000,
  property: { propertyId: 1, propertyName: "Sunrise", addressText: "", ward: "", district: "", city: "" },
  images: [], amenities: [], services: [], equipments: [],
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/deposit" element={<DepositPage />} />
        <Route path="/my-bookings" element={<MyBookingsPage />} />
        <Route path="/payment-success" element={<p>payment success page</p>} />
        <Route path="/payment-result" element={<p>checking server payment status</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  sessionStorage.clear()
  mocks.createLink.mockResolvedValue({ paymentRequestId: 12, bookingId: 7, amount: 3000000, currency: "VND", status: "Pending", checkoutUrl: "https://pay.payos.vn/web/booking-link", expiresAtUtc: "2099-01-01T00:00:00Z", reused: true })
  mocks.getRoom.mockResolvedValue(room)
  mocks.createBooking.mockResolvedValue(booking())
  mocks.getBooking.mockResolvedValue(booking())
  mocks.payBooking.mockResolvedValue(booking({ status: "Deposited" }))
  mocks.getBookings.mockResolvedValue([])
  mocks.getViewingSchedules.mockResolvedValue([])
})

describe("DepositPage", () => {
  it("holds the room and opens a real payOS checkout once without calling the simulated payment API", async () => {
    renderAt("/deposit?roomId=501")
    await screen.findByText("A101 · Studio")
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))

    await screen.findByText("Thanh toán cọc qua payOS")
    expect(mocks.createBooking).toHaveBeenCalledWith("customer-token", {
      roomId: 501,
      moveInDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      occupantCount: 1,
    })
    expect(mocks.getBooking).toHaveBeenCalledWith("customer-token", 7, expect.any(AbortSignal))

    const payButton = screen.getByRole("button", { name: /^Thanh toán/ })
    fireEvent.click(payButton)
    fireEvent.click(payButton)
    await waitFor(() => expect(mocks.openCheckout).toHaveBeenCalledWith("https://pay.payos.vn/web/booking-link"))
    expect(mocks.createLink).toHaveBeenCalledTimes(1)
    expect(mocks.createLink).toHaveBeenCalledWith("customer-token", 7)
    expect(mocks.payBooking).not.toHaveBeenCalled()
    expect(screen.queryByLabelText("MoMo")).toBeNull()
    expect(screen.queryByText("payment success page")).toBeNull()
  })

  it("explains before the hold that the deposit is the first month's rent and holds the room until moveInDate + 3 days, updating dynamically and validating empty moveInDate (C10 items 7 & 9)", async () => {
    renderAt("/deposit?roomId=501")
    await screen.findByText("A101 · Studio")

    fireEvent.change(screen.getByLabelText("Ngày dọn vào"), { target: { value: "2026-11-20" } })
    const rules = screen.getByText(/được trừ vào tiền phòng tháng đầu tiên/)
    expect(rules.textContent).toContain("Phòng được giữ đến hết ngày 23/11/2026.")
    expect(rules.textContent).toContain("không được hoàn")
    expect(rules.textContent).toContain("Nếu chủ trọ hủy, bạn được hoàn 100%")

    fireEvent.change(screen.getByLabelText("Ngày dọn vào"), { target: { value: "" } })
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))
    expect(await screen.findByText("Vui lòng chọn ngày dọn vào.")).toBeTruthy()
    expect(mocks.createBooking).not.toHaveBeenCalled()
  })

  it("validates occupantCount against room.maxOccupants and displays move-in summary (C9)", async () => {
    mocks.getRoom.mockResolvedValue({ ...room, maxOccupants: 2 })
    mocks.getBooking.mockResolvedValue(booking({ moveInDate: "2026-11-20", occupantCount: 2 }))
    renderAt("/deposit?roomId=501")
    await screen.findByText("A101 · Studio")

    fireEvent.change(screen.getByLabelText("Số người ở"), { target: { value: "3" } })
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))
    expect(await screen.findByText("Phòng A101 chỉ được ở tối đa 2 người.")).toBeTruthy()
    expect(mocks.createBooking).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText("Số người ở"), { target: { value: "2" } })
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))
    expect(await screen.findByText("Dọn vào 20/11/2026 · 2 người")).toBeTruthy()
  })

  it("tracks Creating without redirecting to a fabricated success page", async () => {
    mocks.createLink.mockResolvedValue({ paymentRequestId: 12, bookingId: 7, amount: 3000000, currency: "VND", status: "Creating", checkoutUrl: null, expiresAtUtc: null })
    renderAt("/deposit?bookingId=7")
    fireEvent.click(await screen.findByRole("button", { name: /^Thanh toán/ }))
    await screen.findByText("checking server payment status")
    expect(mocks.openCheckout).not.toHaveBeenCalled()
  })

  it("explains when someone else took the room before the hold", async () => {
    mocks.createBooking.mockRejectedValue(new ApiError("Room is not available for booking.", { status: 409 }))
    renderAt("/deposit?roomId=501")
    fireEvent.click(await screen.findByRole("button", { name: "Giữ phòng và thanh toán cọc" }))

    expect((await screen.findByRole("alert")).textContent).toBe("Phòng vừa được người khác giữ hoặc không còn trống.")
    expect(mocks.getBooking).not.toHaveBeenCalled()
  })

  it("does not offer a hold for a room that is no longer listed", async () => {
    mocks.getRoom.mockRejectedValue(new PublicApiError("not-found", "Not found", { status: 404 }))
    renderAt("/deposit?roomId=501")

    expect((await screen.findByRole("alert")).textContent).toBe("Phòng hiện không còn trống để đặt cọc.")
    expect(screen.queryByRole("button", { name: "Giữ phòng và thanh toán cọc" })).toBeNull()
  })

  it.each([
    [booking({ expiryDate: inMinutes(-1) }), "Đã quá thời gian giữ phòng. Vui lòng đặt lại nếu phòng vẫn còn trống."],
    [booking({ status: "Deposited" }), "Booking này đã được thanh toán cọc."],
    [booking({ status: "Cancelled" }), "Booking này đã bị huỷ."],
  ])("does not offer payment for a booking that is no longer awaiting it", async (current, message) => {
    mocks.getBooking.mockResolvedValue(current)
    renderAt("/deposit?bookingId=7")

    expect((await screen.findByRole("alert")).textContent).toBe(message)
    expect(screen.queryByText("Phương thức thanh toán")).toBeNull()
  })
})

describe("MyBookingsPage", () => {
  it("labels expired holds and lets the customer pay or cancel only an active hold", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({ bookingId: 9, roomCode: "B202", status: "Deposited", expiryDate: inMinutes(7 * 24 * 60) }),
      booking({ bookingId: 8, roomCode: "A102", expiryDate: inMinutes(-5) }),
      booking({ bookingId: 7 }),
    ])
    mocks.cancelBooking.mockResolvedValue(booking({ status: "Cancelled" }))
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    renderAt("/my-bookings")

    await screen.findByText("Booking #7")
    expect(screen.getByText("Đã đặt cọc")).toBeTruthy()
    expect(screen.getByText("Hết hạn thanh toán")).toBeTruthy()
    expect(screen.getByText("Chờ thanh toán")).toBeTruthy()
    expect(screen.getByRole("link", { name: "Thanh toán cọc" }).getAttribute("href")).toBe("/deposit?bookingId=7")
    const cancelButton = screen.getByRole("button", { name: "Huỷ giữ phòng" })

    fireEvent.click(cancelButton)
    expect(mocks.cancelBooking).not.toHaveBeenCalled()

    fireEvent.click(cancelButton)
    await screen.findByText("Đã huỷ")
    expect(confirm).toHaveBeenCalledTimes(2)
    expect(mocks.cancelBooking).toHaveBeenCalledWith("customer-token", 7)
    expect(screen.queryByRole("button", { name: "Huỷ giữ phòng" })).toBeNull()
  })

  it("tells the customer how long a deposited room is held and that a missed move-in keeps the deposit", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({ bookingId: 9, status: "Deposited", holdUntilUtc: "2026-11-21T03:00:00Z", canReleaseRoom: false }),
    ])
    renderAt("/my-bookings")

    const hold = await screen.findByText(/Phòng được giữ đến/)
    expect(hold.textContent).toContain("Giữ phòng đến hết 21/11/2026")
    expect(hold.textContent).toContain("tiền cọc không được hoàn")
    expect(screen.queryByText(/giữ đến khi lập hợp đồng/)).toBeNull()
  })

  it("hides 'Xin hoàn cọc', lets customer cancel a deposited booking with loss-of-deposit confirmation, and shows refund contact box + returned status + cancellation reason (C10 & C11)", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({
        bookingId: 9,
        roomCode: "B202",
        status: "Deposited",
        depositAmount: 3_000_000,
        canRequestRefund: false,
        refundStatus: null,
      }),
      booking({
        bookingId: 10,
        roomCode: "C303",
        status: "Cancelled",
        depositAmount: 3_500_000,
        cancellationReason: "LandlordCancelled",
        depositRefundDue: true,
        contacts: [
          {
            fullName: "Trần Quản Lý",
            phoneNumber: "0912345678",
            email: "manager@boarding.vn",
            role: "Manager",
          },
        ],
      }),
      booking({
        bookingId: 11,
        roomCode: "D404",
        status: "Cancelled",
        depositAmount: 4_000_000,
        cancellationReason: "LandlordCancelled",
        depositRefundDue: false,
        depositReturnedAtUtc: "2026-11-16T03:00:00Z",
        depositReturnedByName: "Quản lý Bình",
      }),
    ])
    mocks.cancelBooking.mockResolvedValue(
      booking({
        bookingId: 9,
        roomCode: "B202",
        status: "Cancelled",
        cancellationReason: "CustomerCancelled",
      }),
    )
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    renderAt("/my-bookings")

    await screen.findByText("Booking #9")
    // 1. Nút Xin hoàn cọc bị ẩn
    expect(screen.queryByRole("button", { name: /Xin hoàn cọc/ })).toBeNull()

    // 4 & 5. Lý do hủy + khung liên hệ nhận lại tiền cọc + trạng thái đã xác nhận hoàn cọc (C11)
    expect(screen.getAllByText(/Lý do hủy: Quản lý \/ chủ trọ hủy/)).toHaveLength(2)
    expect(screen.getByText(/Bạn được hoàn 100% tiền cọc/)).toBeTruthy()
    expect(screen.getByText(/Liên hệ để nhận lại tiền:/)).toBeTruthy()
    expect(screen.getByText(/Trần Quản Lý/)).toBeTruthy()
    expect(screen.getByText(/Quản lý đã xác nhận hoàn .* lúc/)).toBeTruthy()
    const telLink = screen.getByRole("link", { name: "Gọi/Zalo: 0912345678" })
    expect(telLink.getAttribute("href")).toBe("tel:0912345678")
    const mailLink = screen.getByRole("link", { name: "manager@boarding.vn" })
    expect(mailLink.getAttribute("href")).toBe("mailto:manager@boarding.vn")

    // 2. Khách hủy booking đã cọc -> xác nhận mất cọc
    const cancelDepositBtn = screen.getByRole("button", { name: "Hủy đặt cọc" })
    fireEvent.click(cancelDepositBtn)
    expect(confirm.mock.calls[0][0]).toContain("Nếu hủy, bạn sẽ mất tiền cọc")
    expect(confirm.mock.calls[0][0]).toContain("Bạn chắc chắn?")
    expect(mocks.cancelBooking).not.toHaveBeenCalled()

    fireEvent.click(cancelDepositBtn)
    await screen.findByText("Đã hủy đặt cọc phòng B202.")
    expect(mocks.cancelBooking).toHaveBeenCalledWith("customer-token", 9)
    expect(screen.getByText(/Lý do hủy: Khách hủy đặt cọc/)).toBeTruthy()
  })

  it("shows the customer's viewing requests above the deposit holds", async () => {
    mocks.getViewingSchedules.mockResolvedValue([
      { viewingId: 3, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise", roomTypeId: 2, roomTypeName: "Studio", currentMonthlyRent: 3_000_000, customerId: 10,
        customerName: "Customer A", customerPhone: "0900", scheduledAt: null, status: "Pending" },
    ])
    renderAt("/my-bookings")

    expect(await screen.findByText("Sunrise · Studio")).toBeTruthy()
    expect(screen.getByText(/Giá tham khảo hiện tại: 3\.000\.000/)).toBeTruthy()
    expect(screen.getByText("Thời gian: Chờ Admin phân lịch")).toBeTruthy()
    expect(screen.getByText("Chờ xác nhận")).toBeTruthy()
  })

  it("navigates from an approved viewing to DepositPage with viewingId so the customer can choose moveInDate and occupantCount (C10 item 6)", async () => {
    mocks.getViewingSchedules.mockResolvedValue([
      {
        viewingId: 3,
        roomId: 501,
        roomCode: "A101",
        propertyId: 1,
        propertyName: "Sunrise",
        roomTypeId: 2,
        roomTypeName: "Studio",
        currentMonthlyRent: 3_000_000,
        customerId: 10,
        customerName: "Customer A",
        customerPhone: "0900",
        scheduledAt: "2026-11-16T03:00:00Z",
        status: "Approved",
      },
    ])
    renderAt("/my-bookings")

    fireEvent.click(await screen.findByRole("button", { name: "Đặt cọc phòng này" }))
    await screen.findByText("A101 · Studio")
    fireEvent.change(screen.getByLabelText("Số người ở"), { target: { value: "2" } })
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))

    await screen.findByText("Thanh toán cọc qua payOS")
    expect(mocks.createBooking).toHaveBeenCalledWith("customer-token", {
      roomId: 501,
      viewingId: 3,
      moveInDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      occupantCount: 2,
    })
  })

  it("retries after a failed load", async () => {
    mocks.getBookings.mockRejectedValueOnce(new ApiError("Không thể kết nối đến máy chủ.")).mockResolvedValueOnce([])
    renderAt("/my-bookings")

    fireEvent.click(await screen.findByRole("button", { name: "Thử lại" }))

    await screen.findByText("Bạn chưa đặt cọc phòng nào.")
    expect(mocks.getBookings).toHaveBeenCalledTimes(2)
  })
})
