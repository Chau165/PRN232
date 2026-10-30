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
}
