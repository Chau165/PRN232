import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"

type BookingTiming = Pick<BookingDto, "status" | "expiryDate">

// BE vẫn để Status = Pending khi quá 15 phút (phòng đã tự được nhả), nên FE tự suy ra "hết hạn".
// Bằng đúng hạn cũng tính là hết hạn, khớp quy ước D3 của BE.
export function isBookingExpired(booking: BookingTiming, now = Date.now()) {
  return booking.status === "Pending" && Date.parse(booking.expiryDate) <= now
}

export function bookingStatusDisplay(booking: BookingTiming & { contractId?: number | null }, now = Date.now()) {
  if (booking.contractId) return { label: "Đã lập hợp đồng", tone: "green" as const }
  if (booking.status === "Deposited") return { label: "Đã đặt cọc", tone: "teal" as const }
  if (booking.status === "Cancelled") return { label: "Đã huỷ", tone: "rose" as const }
  return isBookingExpired(booking, now)
    ? { label: "Hết hạn thanh toán", tone: "slate" as const }
    : { label: "Chờ thanh toán", tone: "amber" as const }
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(value))
}

export const BOOKING_MAX_MOVE_IN_DAYS = 14
export const BOOKING_OCCUPANTS_MIN = 1
export const BOOKING_OCCUPANTS_MAX = 10

export function getVietnamIsoDateOffset(
  daysOffset = 0,
  now: Date = new Date(),
): string {
  const vnTime = new Date(
    now.getTime() + 7 * 60 * 60 * 1000 + daysOffset * 24 * 60 * 60 * 1000,
  )
  return vnTime.toISOString().slice(0, 10)
}

export function bookingMoveInDateError(
  moveInDate: string | null | undefined,
  today: string = getVietnamIsoDateOffset(0),
  maxDate: string = getVietnamIsoDateOffset(BOOKING_MAX_MOVE_IN_DAYS),
): string | null {
  const trimmed = moveInDate?.trim() ?? ""
  if (!trimmed) {
    return "Ngày dọn vào không được ở quá khứ."
  }
  if (trimmed < today) {
    return "Ngày dọn vào không được ở quá khứ."
  }
  if (trimmed > maxDate) {
    return "Ngày dọn vào tối đa 14 ngày kể từ hôm nay."
  }
  return null
}

export function bookingOccupantCountError(
  occupantCount: number | string | null | undefined,
  room?: { roomCode: string; maxOccupants?: number | null },
): string | null {
  if (
    occupantCount === null ||
    occupantCount === undefined ||
    (typeof occupantCount === "string" && occupantCount.trim() === "")
  ) {
    return "Số người ở phải từ 1 đến 10."
  }
  const value =
    typeof occupantCount === "number" ? occupantCount : Number(occupantCount)
  if (
    !Number.isInteger(value) ||
    value < BOOKING_OCCUPANTS_MIN ||
    value > BOOKING_OCCUPANTS_MAX
  ) {
    return "Số người ở phải từ 1 đến 10."
  }
  if (
    room &&
    room.maxOccupants !== undefined &&
    room.maxOccupants !== null &&
    value > room.maxOccupants
  ) {
    return `Phòng ${room.roomCode} chỉ được ở tối đa ${room.maxOccupants} người.`
  }
  return null
}

export function formatBookingMoveInSummary(
  booking: Pick<BookingDto, "moveInDate" | "occupantCount">,
): string | null {
  const rawDate = booking.moveInDate?.trim() ?? ""
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(rawDate)
  const datePart = match
    ? `Dọn vào ${match[3]}/${match[2]}/${match[1]}`
    : rawDate
      ? `Dọn vào ${rawDate}`
      : null
  const occupantPart =
    booking.occupantCount !== undefined &&
    booking.occupantCount !== null &&
    booking.occupantCount > 0
      ? `${booking.occupantCount} người`
      : null

  if (datePart && occupantPart) return `${datePart} · ${occupantPart}`
  return datePart ?? occupantPart
}

// 409 nghĩa là trạng thái phòng/booking vừa đổi; mỗi màn hình tự giải thích bằng câu phù hợp.
export function bookingErrorMessage(error: unknown, conflictMessage: string) {
  if (error instanceof ApiError && error.status === 409) return conflictMessage
  return error instanceof Error ? error.message : "Yêu cầu không thành công. Vui lòng thử lại."
}
