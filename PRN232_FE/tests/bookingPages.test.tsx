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
  createBookingPaymentLink: vi.fn(),
  cancelBooking: vi.fn(),
  getRoom: vi.fn(),
  getViewingSchedules: vi.fn(),
  openCheckout: vi.fn(),
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
  getBookings: mocks.getBookings,
  getBooking: mocks.getBooking,
  createBooking: mocks.createBooking,
  createBookingPaymentLink: mocks.createBookingPaymentLink,
  cancelBooking: mocks.cancelBooking,
}))
vi.mock("@/utils/invoicePayment", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/utils/invoicePayment")>()),
  openPayOSCheckout: mocks.openCheckout,
}))
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
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  mocks.getRoom.mockResolvedValue(room)
  mocks.createBooking.mockResolvedValue(booking())
  mocks.getBooking.mockResolvedValue(booking())
  sessionStorage.clear()
  mocks.openCheckout.mockReset()
  mocks.createBookingPaymentLink.mockResolvedValue({
    paymentRequestId: 31, bookingId: 7, status: "Pending", amount: 3_000_000, currency: "VND",
    checkoutUrl: "https://pay.payos.vn/web/deposit-31", expiresAtUtc: inMinutes(15), reused: false,
  })
  mocks.getBookings.mockResolvedValue([])
  mocks.getViewingSchedules.mockResolvedValue([])
})

describe("DepositPage", () => {
  it("holds the room, then opens the payOS checkout once for the deposit", async () => {
    renderAt("/deposit?roomId=501")
    await screen.findByText("A101 · Studio")
    fireEvent.click(screen.getByRole("button", { name: "Giữ phòng và thanh toán cọc" }))

    const payButton = await screen.findByRole("button", { name: /qua payOS$/ })
    expect(mocks.createBooking).toHaveBeenCalledWith("customer-token", 501)
    expect(mocks.getBooking).toHaveBeenCalledWith("customer-token", 7, expect.any(AbortSignal))

    fireEvent.click(payButton)
    fireEvent.click(payButton)
    await waitFor(() => expect(mocks.openCheckout).toHaveBeenCalledWith("https://pay.payos.vn/web/deposit-31"))
    expect(mocks.createBookingPaymentLink).toHaveBeenCalledTimes(1)
    expect(mocks.createBookingPaymentLink).toHaveBeenCalledWith("customer-token", 7)
    expect(JSON.parse(sessionStorage.getItem("troviet-invoice-payment:10") ?? "null"))
      .toEqual({ requestId: 31, bookingId: 7, providerLinkId: "deposit-31" })
  })

  it("goes to the verified result page when the deposit link cannot be opened", async () => {
    mocks.createBookingPaymentLink.mockResolvedValue({
      paymentRequestId: 31, bookingId: 7, status: "Succeeded", amount: 3_000_000, currency: "VND",
      checkoutUrl: null, expiresAtUtc: null, reused: true,
    })
    render(
      <MemoryRouter initialEntries={["/deposit?bookingId=7"]}>
        <Routes>
          <Route path="/deposit" element={<DepositPage />} />
          <Route path="/payment-result" element={<p>payment result page</p>} />
        </Routes>
      </MemoryRouter>,
    )
    fireEvent.click(await screen.findByRole("button", { name: /qua payOS$/ }))
    await screen.findByText("payment result page")
    expect(mocks.openCheckout).not.toHaveBeenCalled()
  })

  it("explains when payOS is not available for deposits", async () => {
    mocks.createBookingPaymentLink.mockRejectedValue(new ApiError("Checkout unavailable", { status: 503 }))
    renderAt("/deposit?bookingId=7")
    fireEvent.click(await screen.findByRole("button", { name: /qua payOS$/ }))
    expect((await screen.findByRole("alert")).textContent).toBe(
      "Thanh toán payOS hiện chưa sẵn sàng. Vui lòng thử lại sau ít phút hoặc liên hệ quản lý.",
    )
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
    expect(screen.queryByRole("button", { name: /qua payOS$/ })).toBeNull()
  })
})

describe("MyBookingsPage refunds", () => {
  it("offers a refund only when the BE says the deposit is eligible and shows the refund status", async () => {
    mocks.getBookings.mockResolvedValue([
      booking({ bookingId: 7, status: "Deposited", canRequestRefund: true }),
      booking({ bookingId: 8, roomCode: "B202", status: "Deposited", refundStatus: "PendingReview", canRequestRefund: false }),
      booking({ bookingId: 9, roomCode: "C303", status: "Deposited", contractId: 3 }),
    ])
    renderAt("/my-bookings")
    expect(await screen.findByRole("button", { name: "Xin hoàn cọc" })).toBeTruthy()
    expect(screen.getByRole("button", { name: "Xem yêu cầu hoàn cọc" })).toBeTruthy()
    expect(screen.getByText("Chờ Admin xét duyệt")).toBeTruthy()
    expect(screen.getAllByText("Đến khi lập hợp đồng hoặc hoàn cọc")).toHaveLength(2)
    // Booking đang có đơn hoàn thì không còn báo "chủ trọ sẽ liên hệ lập hợp đồng".
    expect(screen.getAllByText("Chủ trọ sẽ liên hệ để lập hợp đồng thuê.")).toHaveLength(1)
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

  it("shows the customer's viewing requests above the deposit holds", async () => {
    mocks.getViewingSchedules.mockResolvedValue([
      { viewingId: 3, roomId: 501, roomCode: "A101", propertyId: 1, propertyName: "Sunrise", customerId: 10,
        customerName: "Customer A", customerPhone: "0900", scheduledAt: null, status: "Pending" },
    ])
    renderAt("/my-bookings")

    expect(await screen.findByText("Sunrise · Phòng A101")).toBeTruthy()
    expect(screen.getByText("Thời gian: Chờ Admin phân lịch")).toBeTruthy()
    expect(screen.getByText("Chờ xác nhận")).toBeTruthy()
  })

  it("retries after a failed load", async () => {
    mocks.getBookings.mockRejectedValueOnce(new ApiError("Không thể kết nối đến máy chủ.")).mockResolvedValueOnce([])
    renderAt("/my-bookings")

    fireEvent.click(await screen.findByRole("button", { name: "Thử lại" }))

    await screen.findByText("Bạn chưa đặt cọc phòng nào.")
    expect(mocks.getBookings).toHaveBeenCalledTimes(2)
  })
})
