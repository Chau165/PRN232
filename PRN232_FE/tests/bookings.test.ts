import { describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import { cancelBooking, createBooking, getBooking, getBookings, payBooking } from "@/api/bookingsApi"
import { bookingErrorMessage, bookingStatusDisplay, isBookingExpired } from "@/utils/bookingDisplay"

function response(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } })
}

describe("Booking API client", () => {
  it("loads the booking list and detail with the current token", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(response([]))
      .mockResolvedValueOnce(response({ bookingId: 7 }))
    await getBookings("customer-token")
    await getBooking("customer-token", 7)
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/bookings$/)
    expect(String(fetchMock.mock.calls[1][0])).toMatch(/\/api\/bookings\/7$/)
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer customer-token")
  })

  it("sends only the RoomId and payment method that the API accepts", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({ bookingId: 7 }))
    await createBooking("customer-token", 501)
    await payBooking("customer-token", 7, "MoMo")
    await cancelBooking("customer-token", 7)
    expect(fetchMock.mock.calls.map(([url]) => String(url).replace(/^.*\/api/, "/api"))).toEqual([
      "/api/bookings", "/api/bookings/7/pay", "/api/bookings/7/cancel",
    ])
    expect(fetchMock.mock.calls.every(([, init]) => init?.method === "POST")).toBe(true)
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({ roomId: 501 })
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({ paymentMethod: "MoMo" })
    expect(fetchMock.mock.calls[2][1]?.body).toBeUndefined()
  })
})

describe("Booking display helpers", () => {
  const now = Date.parse("2026-10-28T12:00:00Z")

  it("treats a pending hold as expired from its expiry instant onward", () => {
    expect(isBookingExpired({ status: "Pending", expiryDate: "2026-10-28T12:00:01Z" }, now)).toBe(false)
    expect(isBookingExpired({ status: "Pending", expiryDate: "2026-10-28T12:00:00Z" }, now)).toBe(true)
    expect(isBookingExpired({ status: "Deposited", expiryDate: "2026-10-28T11:00:00Z" }, now)).toBe(false)
  })

  it("labels each booking state in Vietnamese", () => {
    const label = (status: "Pending" | "Deposited" | "Cancelled", expiryDate: string) =>
      bookingStatusDisplay({ status, expiryDate }, now).label
    expect(label("Pending", "2026-10-28T12:15:00Z")).toBe("Chờ thanh toán")
    expect(label("Pending", "2026-10-28T11:45:00Z")).toBe("Hết hạn thanh toán")
    expect(label("Deposited", "2026-11-04T12:00:00Z")).toBe("Đã đặt cọc")
    expect(label("Cancelled", "2026-10-28T12:15:00Z")).toBe("Đã huỷ")
  })

  it("replaces conflict details with the screen's own explanation", () => {
    expect(bookingErrorMessage(new ApiError("Room is not available.", { status: 409 }), "Phòng đã có người giữ.")).toBe("Phòng đã có người giữ.")
    expect(bookingErrorMessage(new ApiError("Không thể kết nối."), "unused")).toBe("Không thể kết nối.")
  })
})
