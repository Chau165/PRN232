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

export interface PaymentRequestStatusDto {
  paymentRequestId: number
  invoiceId: number
  customerId: number
  propertyId: number
  status: PaymentRequestStatus
  invoiceStatus: string
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
