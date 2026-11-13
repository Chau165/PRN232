import { apiRequest } from "@/api/apiClient"
import type {
  CreateRoomRequest,
  ManagementRoomDto,
  RoomImageDto,
  RoomTypeDto,
  UpdateRoomRequest,
} from "@/types/managementRoom"

export type RoomFilters = {
  propertyId?: number
  roomTypeId?: number
  status?: string
  search?: string
}

export function getManagementRooms(
  accessToken: string,
  filters: RoomFilters = {},
  signal?: AbortSignal,
) {
  const params = new URLSearchParams()
  if (filters.propertyId !== undefined)
    params.set("propertyId", String(filters.propertyId))
  if (filters.roomTypeId !== undefined)
    params.set("roomTypeId", String(filters.roomTypeId))
  if (filters.status) params.set("status", filters.status)
  if (filters.search?.trim()) params.set("search", filters.search.trim())
  const query = params.toString()
  return apiRequest<ManagementRoomDto[]>(`/rooms${query ? `?${query}` : ""}`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getManagementRoom(
  accessToken: string,
  roomId: number,
  signal?: AbortSignal,
) {
  return apiRequest<ManagementRoomDto>(`/rooms/${roomId}`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getRoomTypes(accessToken: string, signal?: AbortSignal) {
  return apiRequest<RoomTypeDto[]>("/room-types", {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function createRoom(accessToken: string, request: CreateRoomRequest) {
  return apiRequest<ManagementRoomDto>("/rooms", {
    accessToken,
    method: "POST",
    json: request,
  })
}

export function updateRoom(
  accessToken: string,
  roomId: number,
  request: UpdateRoomRequest,
) {
  return apiRequest<ManagementRoomDto>(`/rooms/${roomId}`, {
    accessToken,
    method: "PUT",
    json: request,
  })
}

export function deleteRoom(accessToken: string, roomId: number) {
  return apiRequest<void>(`/rooms/${roomId}`, {
    accessToken,
    method: "DELETE",
  })
}

export function getRoomImages(
  accessToken: string,
  roomId: number,
  signal?: AbortSignal,
) {
  return apiRequest<RoomImageDto[]>(`/rooms/${roomId}/images`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function uploadRoomImage(
  accessToken: string,
  roomId: number,
  file: File,
  isPrimary: boolean,
) {
  const body = new FormData()
  body.append("file", file)
  body.append("isPrimary", String(isPrimary))
  return apiRequest<RoomImageDto>(`/rooms/${roomId}/images/upload`, {
    accessToken,
    method: "POST",
    body,
  })
}

export function setPrimaryRoomImage(
  accessToken: string,
  roomId: number,
  imageId: number,
) {
  return apiRequest<void>(`/rooms/${roomId}/images/${imageId}/primary`, {
    accessToken,
    method: "PUT",
  })
}

export function deleteRoomImage(
  accessToken: string,
  roomId: number,
  imageId: number,
) {
  return apiRequest<void>(`/rooms/${roomId}/images/${imageId}`, {
    accessToken,
    method: "DELETE",
  })
}
