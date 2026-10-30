export type ViewingStatus = "Pending" | "Approved" | "Rejected"

export interface ViewingScheduleDto {
  viewingId: number
  roomId: number
  roomCode: string
  propertyId: number
  propertyName: string
  customerId: number
  customerName: string
  customerPhone: string
  // null nghĩa là khách để chủ trọ sắp xếp giờ xem.
  scheduledAt: string | null
  status: ViewingStatus
}
