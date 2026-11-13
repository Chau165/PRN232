export type RefundStatus = "PendingReview" | "Approved" | "Refunding" | "Refunded" | "Rejected" | "RequiresReview"
export interface BookingRefundDto {
  bookingRefundRequestId: number
  bookingId: number
  originalPaymentId: number
  requestedAmount: number
  approvedAmount: number | null
  currency: string
  status: RefundStatus
  reason: string
  recipientBankBin: string
  maskedRecipientAccountNumber: string
  recipientAccountName: string | null
  accountNameVerified: boolean
  createdAtUtc: string
  updatedAtUtc: string
  reviewNote: string | null
  reviewedAtUtc: string | null
  refundedAtUtc: string | null
}
export interface CreateBookingRefundDto {
  reason: string
  recipientBankBin: string
  recipientAccountNumber: string
  recipientAccountName: string
}
export interface BookingRefundReviewDto
  extends Omit<BookingRefundDto, "maskedRecipientAccountNumber"> {
  customerId: number
  depositAmount: number
  recipientAccountNumber: string
  reviewedByAdminId: number | null
}
export interface ReviewBookingRefundDto {
  approve: boolean
  approvedAmount: number | null
  reviewNote: string
}
