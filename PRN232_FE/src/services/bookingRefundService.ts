import { apiRequest } from "@/api/apiClient"
import type {
  BookingRefundDto,
  BookingRefundReviewDto,
  BookingRefundStatus,
  CreateBookingRefundInput,
  ReviewBookingRefundInput,
} from "@/types/bookingRefund"

// Lịch sử hoàn cọc của một booking (mới nhất trước). Customer chủ booking, Manager được phân công hoặc Admin.
export function getBookingRefunds(accessToken: string, bookingId: number, signal?: AbortSignal) {
  return apiRequest<BookingRefundDto[]>(`/bookings/${bookingId}/refund-requests`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

// Chỉ Customer. Không tự gửi lại khi mất phản hồi: đọc lại lịch sử trước để tránh tạo hai đơn.
export function createBookingRefund(accessToken: string, bookingId: number, input: CreateBookingRefundInput) {
  return apiRequest<BookingRefundDto>(`/bookings/${bookingId}/refund-requests`, {
    accessToken, method: "POST", json: input, cache: "no-store", requireJson: true,
  })
}

// --- Admin xét duyệt (api/booking-refund-requests) ---

export function getRefundReviews(
  accessToken: string,
  options: { status?: BookingRefundStatus; page?: number; pageSize?: number } = {},
  signal?: AbortSignal,
) {
  const query = new URLSearchParams()
  if (options.status) query.set("status", options.status)
  query.set("page", String(options.page ?? 1))
  query.set("pageSize", String(options.pageSize ?? 20))
  return apiRequest<BookingRefundReviewDto[]>(`/booking-refund-requests?${query}`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

export function getRefundReview(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<BookingRefundReviewDto>(`/booking-refund-requests/${requestId}`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

export function reviewRefund(accessToken: string, requestId: number, input: ReviewBookingRefundInput) {
  return apiRequest<BookingRefundReviewDto>(`/booking-refund-requests/${requestId}/review`, {
    accessToken, method: "POST", json: input, cache: "no-store", requireJson: true,
  })
}
