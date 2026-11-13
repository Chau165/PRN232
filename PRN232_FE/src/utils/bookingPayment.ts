import { ApiError } from "@/api/apiClient"
import { positiveId, rememberPaymentTarget } from "@/utils/invoicePayment"
const key = (userId: string) => `troviet-booking-payment:${userId}`
export function rememberBookingPayment(
  userId: string,
  requestId: number,
  bookingId: number,
  providerLinkId?: string,
) {
  const previous = recalledBookingPayment(userId)
  const linkId =
    providerLinkId ??
    (previous?.requestId === requestId ? previous.providerLinkId : undefined)
  rememberPaymentTarget(userId, "Booking")
  try {
    sessionStorage.setItem(
      key(userId),
      JSON.stringify({ requestId, bookingId, providerLinkId: linkId }),
    )
  } catch {
    /* navigation context only */
  }
}
interface BookingPaymentContext {
  requestId: number
  bookingId: number
  providerLinkId?: string
}
export function recalledBookingPayment(userId: string): BookingPaymentContext | null {
  try {
    const value = JSON.parse(sessionStorage.getItem(key(userId)) ?? "null")
    if (
      value &&
      positiveId(String(value.requestId)) &&
      positiveId(String(value.bookingId))
    )
      return value
  } catch {
    /* storage unavailable */
  }
  return null
}
export function bookingPaymentError(error: unknown) {
  if (error instanceof ApiError && error.status === 503)
    return "Thanh toán cọc payOS chưa sẵn sàng. Vui lòng liên hệ quản lý."
  if (error instanceof ApiError && [403, 404].includes(error.status ?? 0))
    return "Bạn không có quyền thanh toán booking này hoặc booking không còn khả dụng."
  return "Chưa xác nhận được yêu cầu thanh toán cọc. Hãy kiểm tra lại booking; nếu đã chuyển tiền, không thanh toán thêm."
}
