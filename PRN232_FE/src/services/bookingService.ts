import { apiRequest } from "@/api/apiClient"
import type { BookingDto, PaymentMethod } from "@/types/booking"

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

export function payBooking(accessToken: string, bookingId: number, paymentMethod: PaymentMethod) {
  return apiRequest<BookingDto>(`/bookings/${bookingId}/pay`, {
    accessToken,
    method: "POST",
    body: JSON.stringify({ paymentMethod }),
  })
}

export function cancelBooking(accessToken: string, bookingId: number) {
  return apiRequest<BookingDto>(`/bookings/${bookingId}/cancel`, { accessToken, method: "POST" })
}
