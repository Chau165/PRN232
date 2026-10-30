import { apiRequest } from "@/api/apiClient"
import type { ViewingScheduleDto } from "@/types/viewingSchedule"

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
