import { apiRequest } from "@/api/apiClient"
import type {
  BookingRefundDto,
  BookingRefundReviewDto,
  CreateBookingRefundDto,
  ReviewBookingRefundDto,
  RefundStatus,
} from "@/types/bookingRefund"

export function getBookingRefunds(
  accessToken: string,
  bookingId: number,
  signal?: AbortSignal,
) {
  return apiRequest<BookingRefundDto[]>(
    `/bookings/${bookingId}/refund-requests`,
    { accessToken, signal, cache: "no-store", requireJson: true },
  )
}
export function createBookingRefund(
  accessToken: string,
  bookingId: number,
  input: CreateBookingRefundDto,
) {
  // Never send an amount, customer ID or status supplied by the browser.
  return apiRequest<BookingRefundDto>(
    `/bookings/${bookingId}/refund-requests`,
    {
      accessToken,
      method: "POST",
      cache: "no-store",
      requireJson: true,
      json: {
        reason: input.reason,
        recipientBankBin: input.recipientBankBin,
        recipientAccountNumber: input.recipientAccountNumber,
        recipientAccountName: input.recipientAccountName,
      },
    },
  )
}
export function getRefundReviews(
  accessToken: string,
  status: RefundStatus | "",
  page: number,
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ page: String(page), pageSize: "20" })
  if (status) query.set("status", status)
  return apiRequest<BookingRefundReviewDto[]>(
    `/booking-refund-requests?${query}`,
    { accessToken, signal, cache: "no-store", requireJson: true },
  )
}
export function getRefundReview(
  accessToken: string,
  requestId: number,
  signal?: AbortSignal,
) {
  return apiRequest<BookingRefundReviewDto>(
    `/booking-refund-requests/${requestId}`,
    { accessToken, signal, cache: "no-store", requireJson: true },
  )
}
export function reviewBookingRefund(
  accessToken: string,
  requestId: number,
  input: ReviewBookingRefundDto,
) {
  return apiRequest<BookingRefundReviewDto>(
    `/booking-refund-requests/${requestId}/review`,
    {
      accessToken,
      method: "POST",
      cache: "no-store",
      requireJson: true,
      json: {
        approve: input.approve,
        approvedAmount: input.approvedAmount,
        reviewNote: input.reviewNote,
      },
    },
  )
}
