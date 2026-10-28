import type { ViewingScheduleDto } from "@/api/viewingSchedulesApi"
import { formatDateTime } from "@/utils/bookingDisplay"

export function viewingStatusDisplay(viewing: Pick<ViewingScheduleDto, "status">) {
  if (viewing.status === "Approved") return { label: "Đã chấp thuận", tone: "green" as const }
  if (viewing.status === "Rejected") return { label: "Đã từ chối", tone: "rose" as const }
  return { label: "Chờ xác nhận", tone: "amber" as const }
}

export function viewingTimeLabel(viewing: Pick<ViewingScheduleDto, "scheduledAt">) {
  return viewing.scheduledAt ? formatDateTime(viewing.scheduledAt) : "Chủ trọ sắp xếp"
}

// Ô datetime-local trả về giờ địa phương không kèm múi giờ ("2026-10-30T16:00");
// new Date() hiểu đó là giờ của trình duyệt, toISOString() đổi sang UTC cho BE.
export function localInputToIso(value: string) {
  return new Date(value).toISOString()
}

// Chiều ngược lại: đổi một thời điểm sang chuỗi cho ô datetime-local theo giờ địa phương
// (dùng làm giá trị nhỏ nhất, hoặc điền sẵn giờ khách đã chọn).
export function toLocalInput(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}
