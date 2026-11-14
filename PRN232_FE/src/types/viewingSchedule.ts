export type ViewingStatus = "Pending" | "Approved" | "Rejected"

export interface ViewingScheduleDto {
  viewingId: number
  roomId: number | null
  roomCode: string | null
  propertyId: number
  propertyName: string
  roomTypeId: number
  roomTypeName: string
  currentMonthlyRent: number | null
  customerId: number
  customerName: string
  customerPhone: string
  // null nghĩa là khách để chủ trọ sắp xếp giờ xem.
  scheduledAt: string | null
  status: ViewingStatus
  assignedTo: number | null
  assignedManagerName: string | null
  assignedManagerPhone: string | null
  managerNote: string | null
  respondedBy: number | null
  respondedAt: string | null
}

export interface ViewingAvailableRoomDto {
  roomId: number
  roomCode: string
  areaSqm: number
  status: string
}
