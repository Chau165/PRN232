// Khớp BookingRefundDto / BookingRefundReviewDto của BE (api/bookings/{id}/refund-requests, api/booking-refund-requests).
export type BookingRefundStatus = "PendingReview" | "Approved" | "Refunding" | "Refunded" | "Rejected" | "RequiresReview"

// Customer, Manager được phân công và Admin đọc cùng dạng này; số tài khoản luôn đã che.
export interface BookingRefundDto {
  bookingRefundRequestId: number
  bookingId: number
  originalPaymentId: number
  requestedAmount: number
  currency: string
  status: BookingRefundStatus
  reason: string
  recipientBankBin: string
  maskedRecipientAccountNumber: string
  recipientAccountName: string | null
  accountNameVerified: boolean
  createdAtUtc: string
  updatedAtUtc: string
  approvedAmount: number | null
  reviewNote: string | null
  reviewedAtUtc: string | null
  refundedAtUtc: string | null
}

// Số tiền do BE tự xác định từ khoản cọc gốc; FE không gửi số tiền.
export interface CreateBookingRefundInput {
  reason: string
  recipientBankBin: string
  recipientAccountNumber: string
  recipientAccountName?: string
}

// Chỉ Admin: có số tài khoản đầy đủ để đối chiếu trước khi duyệt. Không lưu bền dữ liệu này ở FE.
export interface BookingRefundReviewDto {
  bookingRefundRequestId: number
  bookingId: number
  customerId: number
  originalPaymentId: number
  depositAmount: number
  requestedAmount: number
  approvedAmount: number | null
  currency: string
  status: BookingRefundStatus
  reason: string
  recipientBankBin: string
  recipientAccountNumber: string
  recipientAccountName: string | null
  accountNameVerified: boolean
  reviewedByAdminId: number | null
  reviewNote: string | null
  createdAtUtc: string
  updatedAtUtc: string
  reviewedAtUtc: string | null
  refundedAtUtc: string | null
}

// Duyệt: approvedAmount là số nguyên dương <= requestedAmount. Từ chối: approvedAmount phải là null.
export interface ReviewBookingRefundInput {
  approve: boolean
  approvedAmount: number | null
  reviewNote: string
}
