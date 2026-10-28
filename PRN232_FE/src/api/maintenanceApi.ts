import { apiRequest } from "@/api/apiClient"

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

export function getMaintenanceRequests(accessToken: string, signal?: AbortSignal) {
  return apiRequest<MaintenanceRequestDto[]>("/maintenance-requests", { accessToken, signal, cache: "no-store" })
}

export function getMaintenanceRequest(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<MaintenanceRequestDto>(`/maintenance-requests/${requestId}`, { accessToken, signal, cache: "no-store" })
}

export function getMaintenanceLogs(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<MaintenanceLogDto[]>(`/maintenance-requests/${requestId}/logs`, { accessToken, signal, cache: "no-store" })
}

export function assignMaintenanceRequest(
  accessToken: string,
  requestId: number,
  assignment: { assignedTo: number; assignmentNote: string },
) {
  return apiRequest<MaintenanceRequestDto>(`/maintenance-requests/${requestId}`, {
    accessToken,
    method: "PATCH",
    body: JSON.stringify(assignment),
  })
}

export function submitMaintenanceReport(
  accessToken: string,
  requestId: number,
  form: FormData,
) {
  return apiRequest<MaintenanceReportDto>(`/maintenance-requests/${requestId}/reports`, {
    accessToken,
    method: "POST",
    body: form,
  })
}

export function createMaintenanceRequest(accessToken: string, request: CreateMaintenanceRequest, signal?: AbortSignal) {
  return apiRequest<MaintenanceRequestDto>("/maintenance-requests", {
    accessToken,
    signal,
    method: "POST",
    body: JSON.stringify(request),
  })
}
