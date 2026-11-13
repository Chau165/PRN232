export type BookingStatus = "Pending" | "Deposited" | "Cancelled"
export type PaymentMethod = "VNPay" | "MoMo"

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
  refundStatus?: string | null
  canRequestRefund?: boolean
  latestPaymentRequestId?: number | null
  // OD07: deposit time + 7 days, only for a deposited booking without a contract.
  holdUntilUtc?: string | null
  // True once that hold is over and no refund request is open; staff may then release the room.
  canReleaseRoom?: boolean
}
