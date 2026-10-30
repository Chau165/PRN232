import { apiRequest } from "@/api/apiClient"
import type {
  CreateMaintenanceRequest,
  MaintenanceLogDto,
  MaintenanceReportDto,
  MaintenanceRequestDto,
} from "@/types/maintenance"

export function getMaintenanceRequests(accessToken: string, signal?: AbortSignal) {
  return apiRequest<MaintenanceRequestDto[]>("/maintenance-requests", { accessToken, signal, cache: "no-store" })
}

export function getMaintenanceRequest(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<MaintenanceRequestDto>(`/maintenance-requests/${requestId}`, { accessToken, signal, cache: "no-store" })
}

export function getMaintenanceLogs(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<MaintenanceLogDto[]>(`/maintenance-requests/${requestId}/logs`, { accessToken, signal, cache: "no-store" })
}

export function getMaintenanceReport(accessToken: string, requestId: number, signal?: AbortSignal) {
  return apiRequest<MaintenanceReportDto>(`/maintenance-requests/${requestId}/reports`, { accessToken, signal, cache: "no-store" })
}

export function assignMaintenanceRequest(
  accessToken: string,
  requestId: number,
  assignment: { assignedTo: number; assignmentNote: string },
) {
  return apiRequest<MaintenanceRequestDto>(`/maintenance-requests/${requestId}`, {
    accessToken,
    method: "PATCH",
    json: assignment,
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

export function createMaintenanceRequest(
  accessToken: string,
  request: CreateMaintenanceRequest,
  signal?: AbortSignal,
) {
  return apiRequest<MaintenanceRequestDto>("/maintenance-requests", {
    accessToken,
    signal,
    method: "POST",
    json: request,
  })
}
