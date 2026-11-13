export type PaymentReviewStatus = "Open" | "Resolved"
export type PaymentReviewResolution = "RefundedExternally" | "NotDuplicate"

export interface PaymentReviewDto {
  id: number
  invoiceId: number
  paymentRequestId: number
  reason: string
  status: PaymentReviewStatus
  additionalAmount: number
  currency: string
  providerReference: string
  source: string
  transactionDateTime: string | null
  existingPaymentId: number
  existingPaymentMethod: string
  existingAmount: number
  existingPaidAtUtc: string | null
  createdAtUtc: string
  resolution: string | null
  resolutionNote: string | null
  evidenceReference: string | null
  resolvedByAdminId: number | null
  resolvedAtUtc: string | null
}

export interface ResolvePaymentReviewInput {
  resolution: PaymentReviewResolution
  note: string
  evidenceReference: string | null
}
