import { apiRequest } from "@/api/apiClient"
import type { ViewingAvailableRoomDto, ViewingScheduleDto } from "@/types/viewingSchedule"

export function getViewingSchedules(accessToken: string, signal?: AbortSignal) {
  return apiRequest<ViewingScheduleDto[]>("/viewing-schedules", { accessToken, signal, cache: "no-store" })
}

export function createViewingSchedule(accessToken: string, request: {
  roomId?: number | null
  propertyId?: number
  roomTypeId?: number
  scheduledAt: string | null
}) {
  return apiRequest<ViewingScheduleDto>("/viewing-schedules", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function getAvailableViewingRooms(accessToken: string, viewingId: number, signal?: AbortSignal) {
  return apiRequest<ViewingAvailableRoomDto[]>(`/viewing-schedules/${viewingId}/available-rooms`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function selectViewingRoom(accessToken: string, viewingId: number, roomId: number) {
  return apiRequest<ViewingScheduleDto>(`/viewing-schedules/${viewingId}/selected-room`, {
    accessToken,
    method: "PUT",
    json: { roomId },
  })
}

export function approveViewingSchedule(
  accessToken: string,
  viewingId: number,
  request: { scheduledAt: string | null; assignedTo: number; managerNote: string | null },
) {
  return apiRequest<ViewingScheduleDto>(`/viewing-schedules/${viewingId}/approve`, {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function rejectViewingSchedule(accessToken: string, viewingId: number, reason: string) {
  return apiRequest<ViewingScheduleDto>(`/viewing-schedules/${viewingId}/reject`, {
    accessToken,
    method: "POST",
    body: JSON.stringify({ reason }),
  })
}
