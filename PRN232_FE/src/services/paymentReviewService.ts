import { apiRequest } from "@/api/apiClient"
import type { PaymentReviewDto, PaymentReviewStatus, ResolvePaymentReviewInput } from "@/types/paymentReview"

export function getPaymentReviews(
  accessToken: string,
  options: { status?: PaymentReviewStatus; page?: number; pageSize?: number } = {},
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({
    status: options.status ?? "Open",
    page: String(options.page ?? 1),
    pageSize: String(options.pageSize ?? 20),
  })
  return apiRequest<PaymentReviewDto[]>(`/payment-reviews?${query}`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

export function getPaymentReview(accessToken: string, id: number, signal?: AbortSignal) {
  return apiRequest<PaymentReviewDto>(`/payment-reviews/${id}`, {
    accessToken, signal, cache: "no-store", requireJson: true,
  })
}

export function resolvePaymentReview(accessToken: string, id: number, input: ResolvePaymentReviewInput) {
  return apiRequest<PaymentReviewDto>(`/payment-reviews/${id}/resolve`, {
    accessToken, method: "POST", json: input, cache: "no-store", requireJson: true,
  })
}
