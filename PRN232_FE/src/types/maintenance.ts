export interface MaintenanceRequestDto {
  requestId: number
  description: string
  status: "Pending" | "Approved" | "Completed"
  contractId: number
  customerId: number
  customerName: string | null
  roomId: number
  roomCode: string | null
  equipmentId: number | null
  equipmentName: string | null
  equipmentStatus: string | null
  assignedTo: number | null
  assignedStaffName: string | null
  assignmentNote?: string | null
}

export interface MaintenanceReportDto {
  reportId: number
  requestId: number
  description: string
  reportedBy: number
  reportedByName: string | null
  createdAt: string
  images: Array<{ imageId: number; imageUrl: string }>
}

export interface MaintenanceLogDto {
  logId: number
  requestId: number
  oldStatus: string | null
  newStatus: string
  changedBy: number | null
  changedByName: string | null
  changedAt: string
}

export interface CreateMaintenanceRequest {
  contractId: number
  equipmentId: number | null
  description: string
}
