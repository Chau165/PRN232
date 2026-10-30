export interface ContractDto {
  contractId: number
  bookingId: number | null
  roomId: number
  roomCode: string
  propertyId: number
  propertyName: string
  customerId: number
  customerName: string
  customerPhone: string
  // Ngày theo lịch, dạng "yyyy-MM-dd" (DateOnly bên BE).
  startDate: string
  endDate: string
  monthlyRent: number
  depositAmount: number
  status: string
  processedByStaffName: string | null
}
