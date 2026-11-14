import { apiRequest } from "@/api/apiClient"
import type { PropertyRoomTypeDto } from "@/types/propertyRoomType"

export function getPropertyRoomTypes(
  accessToken: string,
  propertyId: number,
  signal?: AbortSignal,
) {
  return apiRequest<PropertyRoomTypeDto[]>(`/properties/${propertyId}/room-types`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function createPropertyRoomType(
  accessToken: string,
  propertyId: number,
  request: { roomTypeId: number; monthlyRent: number },
) {
  return apiRequest<PropertyRoomTypeDto>(`/properties/${propertyId}/room-types`, {
    accessToken,
    method: "POST",
    json: request,
  })
}

export function updatePropertyRoomType(
  accessToken: string,
  propertyId: number,
  roomTypeId: number,
  request: { monthlyRent: number },
) {
  return apiRequest<PropertyRoomTypeDto>(
    `/properties/${propertyId}/room-types/${roomTypeId}`,
    { accessToken, method: "PUT", json: request },
  )
}

export function setPropertyRoomTypeActive(
  accessToken: string,
  propertyId: number,
  roomTypeId: number,
  isActive: boolean,
) {
  return apiRequest<PropertyRoomTypeDto>(
    `/properties/${propertyId}/room-types/${roomTypeId}/status`,
    { accessToken, method: "PATCH", json: { isActive } },
  )
}
