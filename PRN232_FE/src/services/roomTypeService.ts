import { apiRequest } from "@/api/apiClient"

export interface RoomTypeServiceDto {
  serviceId: number
  name: string
  price: number
}

export function getRoomTypeServices(accessToken: string, roomTypeId: number, signal?: AbortSignal) {
  return apiRequest<RoomTypeServiceDto[]>(`/room-types/${roomTypeId}/services`, {
    accessToken, signal, cache: "no-store",
  })
}

export function addRoomTypeService(accessToken: string, roomTypeId: number, serviceId: number) {
  return apiRequest<void>(`/room-types/${roomTypeId}/services/${serviceId}`, {
    accessToken, method: "POST",
  })
}

export function removeRoomTypeService(accessToken: string, roomTypeId: number, serviceId: number) {
  return apiRequest<void>(`/room-types/${roomTypeId}/services/${serviceId}`, {
    accessToken, method: "DELETE",
  })
}
