export type PaymentRequestStatus = "Creating" | "Pending" | "Succeeded" | "Cancelled" | "Expired" | "Create-failed" | "RequiresReview"

export interface InvoicePaymentLinkDto {
  paymentRequestId: number
  invoiceId: number
  status: PaymentRequestStatus
  amount: number
  currency: string
  checkoutUrl: string | null
  expiresAtUtc: string | null
  reused: boolean
}

// GET /api/payment-requests/{id}: dùng chung cho thanh toán hoá đơn và tiền cọc booking.
// Yêu cầu của hoá đơn có invoiceId; yêu cầu tiền cọc có bookingId (invoiceId/invoiceStatus là null).
export interface PaymentRequestStatusDto {
  paymentRequestId: number
  invoiceId: number | null
  bookingId?: number | null
  bookingStatus?: string | null
  customerId: number
  propertyId: number
  status: PaymentRequestStatus
  invoiceStatus: string | null
  amount: number
  currency: string
  createdAtUtc: string
  updatedAtUtc: string
  expiresAtUtc: string | null
  confirmedAtUtc: string | null
  paymentId: number | null
  paidAtUtc: string | null
  isExpired: boolean
  canOpenCheckout: boolean
  checkoutUrl: string | null
}
