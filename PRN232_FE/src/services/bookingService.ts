import { apiRequest } from "@/api/apiClient"
import type { BookingDto, PaymentMethod } from "@/types/booking"
import type { PaymentRequestStatus } from "@/types/invoicePayment"

export interface BookingPaymentLinkDto { paymentRequestId: number; bookingId: number; status: PaymentRequestStatus; amount: number; currency: string; checkoutUrl: string | null; expiresAtUtc: string | null; reused: boolean }
export function createBookingPaymentLink(accessToken: string, bookingId: number) {
  return apiRequest<BookingPaymentLinkDto>(`/bookings/${bookingId}/payment-link`, { accessToken, method: "POST", cache: "no-store", requireJson: true })
}

export function getBookings(accessToken: string, signal?: AbortSignal) {
  return apiRequest<BookingDto[]>("/bookings", { accessToken, signal, cache: "no-store" })
}

export function getBooking(accessToken: string, bookingId: number, signal?: AbortSignal) {
  return apiRequest<BookingDto>(`/bookings/${bookingId}`, { accessToken, signal, cache: "no-store" })
}

export function createBooking(
  accessToken: string,
  request: number | { roomId: number; viewingId?: number },
) {
  const body = typeof request === "number" ? { roomId: request } : request
  return apiRequest<BookingDto>("/bookings", {
    accessToken,
    method: "POST",
    body: JSON.stringify(body),
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
