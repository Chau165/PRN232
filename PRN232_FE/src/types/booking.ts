export type BookingStatus = "Pending" | "Deposited" | "Cancelled"
export type PaymentMethod = "VNPay" | "MoMo"
export type BookingCancellationReason =
  | "CustomerCancelled"
  | "LandlordCancelled"
  | "NoShow"

export interface BookingContactDto {
  fullName: string
  phoneNumber?: string | null
  email?: string | null
  role?: "Manager" | "Owner" | string | null
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
  moveInDate?: string | null
  occupantCount?: number | null
  refundStatus?: string | null
  canRequestRefund?: boolean
  latestPaymentRequestId?: number | null
  // C10: giữ đến hết ngày "moveInDate + 3" theo giờ VN (hoặc 7 ngày với booking cũ).
  holdUntilUtc?: string | null
  // True once that hold is over and no refund request is open; staff may then release the room.
  canReleaseRoom?: boolean
  cancellationReason?: BookingCancellationReason | string | null
  cancelledAtUtc?: string | null
  depositRefundDue?: boolean
  depositReturnedAtUtc?: string | null
  depositReturnedByName?: string | null
  contacts?: BookingContactDto[] | null
}
