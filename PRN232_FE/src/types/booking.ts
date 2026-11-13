import type { BookingRefundStatus } from "@/types/bookingRefund"
import type { PaymentRequestStatus } from "@/types/invoicePayment"

export type BookingStatus = "Pending" | "Deposited" | "Cancelled"

// POST /api/bookings/{id}/payment-link: liên kết checkout payOS cho tiền cọc (BE tái sử dụng link còn hạn).
export interface BookingPaymentLinkDto {
  paymentRequestId: number
  bookingId: number
  status: PaymentRequestStatus
  amount: number
  currency: string
  checkoutUrl: string | null
  expiresAtUtc: string | null
  reused: boolean
}

export interface BookingDto {
  bookingId: number
  roomId: number
  roomCode: string
  propertyId: number
  propertyName: string
  customerId: number
  customerName: string
  depositAmount: number
  status: BookingStatus
  expiryDate: string
  customerPhone: string
  contractId: number | null
  // Gợi ý trạng thái cho UI; mọi thao tác vẫn được BE kiểm tra lại.
  refundStatus?: BookingRefundStatus | null
  canRequestRefund?: boolean
  latestPaymentRequestId?: number | null
}
