import { apiRequest } from "@/api/apiClient"
import type { BookingDto, BookingPaymentLinkDto } from "@/types/booking"

export function getBookings(accessToken: string, signal?: AbortSignal) {
  return apiRequest<BookingDto[]>("/bookings", { accessToken, signal, cache: "no-store" })
}

export function getBooking(accessToken: string, bookingId: number, signal?: AbortSignal) {
  return apiRequest<BookingDto>(`/bookings/${bookingId}`, { accessToken, signal, cache: "no-store" })
}

export function createBooking(accessToken: string, roomId: number) {
  return apiRequest<BookingDto>("/bookings", {
    accessToken,
    method: "POST",
    body: JSON.stringify({ roomId }),
  })
}

// Thay cho API thanh toán giả lập /pay (BE trả 409 khi PayOS.BookingPaymentsEnabled = true).
// Tiền cọc chỉ được ghi nhận khi payOS xác nhận qua webhook/đối soát, không theo URL trả về.
export function createBookingPaymentLink(accessToken: string, bookingId: number) {
  return apiRequest<BookingPaymentLinkDto>(`/bookings/${bookingId}/payment-link`, {
    accessToken, method: "POST", cache: "no-store", requireJson: true,
  })
}

export function cancelBooking(accessToken: string, bookingId: number) {
  return apiRequest<BookingDto>(`/bookings/${bookingId}/cancel`, { accessToken, method: "POST" })
}
