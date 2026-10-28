import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/api/bookingsApi"

type BookingTiming = Pick<BookingDto, "status" | "expiryDate">

// BE vẫn để Status = Pending khi quá 15 phút (phòng đã tự được nhả), nên FE tự suy ra "hết hạn".
// Bằng đúng hạn cũng tính là hết hạn, khớp quy ước D3 của BE.
export function isBookingExpired(booking: BookingTiming, now = Date.now()) {
  return booking.status === "Pending" && Date.parse(booking.expiryDate) <= now
}

export function bookingStatusDisplay(booking: BookingTiming, now = Date.now()) {
  if (booking.status === "Deposited") return { label: "Đã đặt cọc", tone: "teal" as const }
  if (booking.status === "Cancelled") return { label: "Đã huỷ", tone: "rose" as const }
  return isBookingExpired(booking, now)
    ? { label: "Hết hạn thanh toán", tone: "slate" as const }
    : { label: "Chờ thanh toán", tone: "amber" as const }
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(value))
}

// 409 nghĩa là trạng thái phòng/booking vừa đổi; mỗi màn hình tự giải thích bằng câu phù hợp.
export function bookingErrorMessage(error: unknown, conflictMessage: string) {
  if (error instanceof ApiError && error.status === 409) return conflictMessage
  return error instanceof Error ? error.message : "Yêu cầu không thành công. Vui lòng thử lại."
}
