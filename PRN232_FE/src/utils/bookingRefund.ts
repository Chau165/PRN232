import { ApiError } from "@/api/apiClient"
import type {
  CreateBookingRefundDto,
  RefundStatus,
} from "@/types/bookingRefund"

export const refundLabels: Record<RefundStatus, string> = {
  PendingReview: "Chờ Admin xét duyệt",
  Approved: "Đã duyệt, chờ chuyển tiền",
  Refunding: "Đang xử lý chuyển tiền",
  Refunded: "Đã hoàn cọc",
  Rejected: "Đã từ chối",
  RequiresReview: "Cần kiểm tra hoàn tiền",
}
export function refundDisplay(status: string) {
  const label =
    refundLabels[(status as RefundStatus)] ?? "Trạng thái cần kiểm tra"
  const tone =
    status === "Refunded"
      ? "green"
      : status === "Rejected"
        ? "rose"
        : status === "RequiresReview"
          ? "amber"
          : "teal"
  return { label, tone } as const
}
export function refundBlocksContract(status?: string | null) {
  return Boolean(status && status !== "Rejected")
}
export function refundIsActive(status?: string) {
  return Boolean(status && !["Rejected", "Refunded"].includes(status))
}
export function refundError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 409)
      return "Booking hoặc đơn hoàn đã thay đổi. Hãy tải lại để kiểm tra trước khi gửi tiếp."
    if (error.status === 403 || error.status === 404)
      return "Bạn không có quyền xem booking này hoặc dữ liệu không còn khả dụng."
    if (error.status === 401)
      return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
    if (error.status === 400)
      return "Thông tin chưa hợp lệ. Kiểm tra lý do, số tiền và tài khoản nhận."
  }
  return "Chưa xác nhận được kết quả. Hãy tải lại đơn để kiểm tra trước khi gửi tiếp."
}
export function validateRefund(input: CreateBookingRefundDto): string | null {
  if (!validNote(input.reason))
    return "Nhập lý do từ 1 đến 2000 ký tự, không xuống dòng."
  if (!/^[0-9]{6}$/.test(input.recipientBankBin))
    return "Mã BIN ngân hàng cần đúng 6 chữ số."
  if (!/^[0-9]{1,64}$/.test(input.recipientAccountNumber))
    return "Số tài khoản cần từ 1 đến 64 chữ số, không có khoảng trắng."
  if (
    !input.recipientAccountName ||
    input.recipientAccountName.length > 256 ||
    /[\u0000-\u001f\u007f]/.test(input.recipientAccountName)
  )
    return "Nhập tên chủ tài khoản nhận hợp lệ, tối đa 256 ký tự."
  return null
}
export function validNote(value: string) {
  return Boolean(
    value.trim() &&
      value.trim().length <= 2000 &&
      !/[\u0000-\u001f\u007f]/.test(value),
  )
}
export function maskAccount(value: string) {
  return "****" + value.slice(-4)
}
