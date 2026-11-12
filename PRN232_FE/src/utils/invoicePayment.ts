import { ApiError } from "@/api/apiClient"
import type { PaymentRequestStatusDto } from "@/types/invoicePayment"

export function positiveId(value: string | null): number | null {
  if (!value || !/^[1-9]\d*$/.test(value)) return null
  const id = Number(value)
  return Number.isSafeInteger(id) && id <= 2147483647 ? id : null
}

const key = (userId: string) => `troviet-invoice-payment:${userId}`
// Per-tab, per-user context survives the external checkout round trip. Never store credentials or payment results.
export function rememberInvoicePayment(userId: string, requestId: number, invoiceId: number, providerLinkId?: string) {
  const previous = recalledInvoicePayment(userId)
  const linkId = providerLinkId ?? (previous?.requestId === requestId ? previous.providerLinkId : undefined)
  try { sessionStorage.setItem(key(userId), JSON.stringify({ requestId, invoiceId, providerLinkId: linkId })) } catch { /* storage may be disabled */ }
}

export function recalledInvoicePayment(userId: string): { requestId: number; invoiceId: number; providerLinkId?: string } | null {
  try {
    const item = JSON.parse(sessionStorage.getItem(key(userId)) ?? "null")
    if (item && positiveId(String(item.requestId)) && positiveId(String(item.invoiceId))) return item
  } catch { /* corrupted/disabled storage is not a payment failure */ }
  return null
}

export function validPayOSCheckout(value: string | null): string | null {
  if (!value) return null
  try {
    const url = new URL(value)
    return url.origin === "https://pay.payos.vn" && !url.username && !url.password &&
      /^\/web\/[A-Za-z0-9-]+\/?$/.test(url.pathname) && !url.hash ? url.href : null
  } catch { return null }
}

export function openPayOSCheckout(url: string) {
  const verified = validPayOSCheckout(url)
  if (!verified) throw new Error("Liên kết thanh toán không hợp lệ. Vui lòng kiểm tra lại trạng thái.")
  window.location.assign(verified)
}

export function paymentIsFinal(item: PaymentRequestStatusDto) {
  return item.invoiceStatus === "Paid" || !["Creating", "Pending"].includes(item.status)
}

export function paymentResultText(item: PaymentRequestStatusDto) {
  if (item.status === "RequiresReview") return { title: "Thanh toán cần kiểm tra", detail: "Vui lòng liên hệ quản lý để kiểm tra khoản thanh toán. Không thanh toán thêm khi chưa có xác nhận.", tone: "review" }
  if (item.status === "Succeeded" && item.invoiceStatus === "Paid") return { title: "Thanh toán thành công", detail: "Hệ thống đã xác nhận thanh toán hóa đơn qua payOS.", tone: "success" }
  if (item.invoiceStatus === "Paid") return { title: "Hóa đơn đã được thanh toán", detail: "Hóa đơn đã được ghi nhận thanh toán. Bạn không cần trả thêm.", tone: "success" }
  switch (item.status) {
    case "Creating": return { title: "Đang tạo liên kết thanh toán", detail: "Hệ thống đang xác nhận liên kết với payOS. Vui lòng chờ, chưa cần tạo yêu cầu khác.", tone: "waiting" }
    case "Cancelled": return { title: "Liên kết đã hủy", detail: "Hóa đơn chưa được xác nhận thanh toán. Bạn có thể quay lại hóa đơn để kiểm tra trước khi thanh toán tiếp.", tone: "closed" }
    case "Expired": return { title: "Liên kết đã hết hạn", detail: "Quay lại hóa đơn để kiểm tra và tạo liên kết mới nếu vẫn chưa thanh toán.", tone: "closed" }
    case "Create-failed": return { title: "Chưa tạo được liên kết", detail: "Bạn có thể quay lại hóa đơn để thử tạo liên kết thanh toán mới.", tone: "closed" }
    default: return { title: "Đang chờ xác nhận thanh toán", detail: item.isExpired
      ? "Liên kết đã quá hạn mở checkout. Hệ thống vẫn đang kiểm tra kết quả; nếu đã chuyển tiền, vui lòng không trả thêm."
      : "Nếu bạn đã thanh toán, vui lòng chờ hệ thống xác nhận. Không cần thanh toán lại.", tone: "waiting" }
  }
}

export function invoicePaymentError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.status === 409) return "Hóa đơn hoặc yêu cầu thanh toán đã thay đổi. Vui lòng tải lại hóa đơn và kiểm tra trước khi trả thêm."
    if (error.status === 503) return "Thanh toán payOS hiện chưa sẵn sàng. Vui lòng thử lại sau hoặc liên hệ quản lý."
    if (error.status === 403) return "Bạn không có quyền xem yêu cầu thanh toán này."
    if (error.status === 404) return "Không tìm thấy yêu cầu thanh toán. Vui lòng quay lại hóa đơn."
    if (error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để kiểm tra."
    if (error.status === 429) return "Bạn đang kiểm tra quá nhanh. Vui lòng thử lại sau."
  }
  return "Chưa thể xác nhận kết quả với máy chủ. Nếu đã chuyển tiền, vui lòng không thanh toán lại; kiểm tra lại sau."
}
