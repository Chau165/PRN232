import { apiRequest } from "@/api/apiClient"

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

export function getViewingSchedules(accessToken: string, signal?: AbortSignal) {
  return apiRequest<ViewingScheduleDto[]>("/viewing-schedules", { accessToken, signal, cache: "no-store" })
}

export function createViewingSchedule(accessToken: string, request: { roomId: number; scheduledAt: string | null }) {
  return apiRequest<ViewingScheduleDto>("/viewing-schedules", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function approveViewingSchedule(accessToken: string, viewingId: number, scheduledAt: string | null) {
  return apiRequest<ViewingScheduleDto>(`/viewing-schedules/${viewingId}/approve`, {
    accessToken,
    method: "POST",
    body: JSON.stringify({ scheduledAt }),
  })
}

export function rejectViewingSchedule(accessToken: string, viewingId: number) {
  return apiRequest<ViewingScheduleDto>(`/viewing-schedules/${viewingId}/reject`, { accessToken, method: "POST" })
}
