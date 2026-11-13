import { ApiError } from "@/api/apiClient"
import type { BookingRefundStatus } from "@/types/bookingRefund"

type Tone = "amber" | "teal" | "green" | "rose" | "slate"

// Nhãn theo tài liệu PRN232_BE/docs/booking-refund-frontend-p11.md. "Approved" chỉ là đã duyệt, chưa phải đã nhận tiền.
const refundStatusDisplay: Record<BookingRefundStatus, { label: string; tone: Tone }> = {
  PendingReview: { label: "Chờ Admin xét duyệt", tone: "amber" },
  Approved: { label: "Đã duyệt, chờ chuyển tiền", tone: "teal" },
  Refunding: { label: "Đang xử lý chuyển tiền", tone: "teal" },
  RequiresReview: { label: "Cần kiểm tra hoàn tiền", tone: "amber" },
  Rejected: { label: "Đã từ chối", tone: "rose" },
  Refunded: { label: "Đã hoàn cọc", tone: "green" },
}

export const bookingRefundStatuses = Object.keys(refundStatusDisplay) as BookingRefundStatus[]

export function bookingRefundStatusDisplay(status: string | null | undefined) {
  return status && status in refundStatusDisplay
    ? refundStatusDisplay[status as BookingRefundStatus]
    : { label: status || "Không rõ trạng thái", tone: "slate" as const }
}

// Mã BIN (NAPAS) của các ngân hàng phổ biến; ngân hàng khác nhập mã 6 chữ số.
export const popularBanks: ReadonlyArray<{ bin: string; name: string }> = [
  { bin: "970436", name: "Vietcombank" },
  { bin: "970415", name: "VietinBank" },
  { bin: "970418", name: "BIDV" },
  { bin: "970405", name: "Agribank" },
  { bin: "970407", name: "Techcombank" },
  { bin: "970422", name: "MB Bank" },
  { bin: "970416", name: "ACB" },
  { bin: "970432", name: "VPBank" },
  { bin: "970423", name: "TPBank" },
  { bin: "970403", name: "Sacombank" },
  { bin: "970441", name: "VIB" },
  { bin: "970443", name: "SHB" },
  { bin: "970437", name: "HDBank" },
]

export function bankName(bin: string) {
  return popularBanks.find((bank) => bank.bin === bin)?.name ?? `Mã ngân hàng ${bin}`
}

export function refundErrorMessage(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 409) return "Trạng thái booking hoặc yêu cầu hoàn cọc vừa thay đổi. Vui lòng tải lại để kiểm tra."
    if (error.status === 403) return "Bạn không có quyền thực hiện thao tác này."
    if (error.status === 404) return "Không tìm thấy booking hoặc yêu cầu hoàn cọc."
    if (error.status === 400) return error.message
  }
  return error instanceof Error ? error.message : "Yêu cầu không thành công. Vui lòng thử lại."
}
