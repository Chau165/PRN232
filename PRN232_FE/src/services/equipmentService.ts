import { apiRequest } from "@/api/apiClient"
import type {
  EquipmentDto,
  EquipmentFilters,
  EquipmentWriteRequest,
} from "@/types/equipment"

export function getEquipments(
  accessToken: string,
  filters: EquipmentFilters = {},
  signal?: AbortSignal,
) {
  const params = new URLSearchParams()
  if (filters.roomId !== undefined) params.set("roomId", String(filters.roomId))
  if (filters.propertyId !== undefined)
    params.set("propertyId", String(filters.propertyId))
  if (filters.status) params.set("status", filters.status)
  if (filters.search?.trim()) params.set("search", filters.search.trim())
  if (filters.page !== undefined) params.set("page", String(filters.page))
  if (filters.pageSize !== undefined)
    params.set("pageSize", String(filters.pageSize))
  const query = params.toString()
  return apiRequest<EquipmentDto[]>(`/equipments${query ? `?${query}` : ""}`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getEquipment(
  accessToken: string,
  equipmentId: number,
  signal?: AbortSignal,
) {
  return apiRequest<EquipmentDto>(`/equipments/${equipmentId}`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function createEquipment(
  accessToken: string,
  request: EquipmentWriteRequest & { roomId: number },
) {
  return apiRequest<EquipmentDto>("/equipments", {
    accessToken,
    method: "POST",
    json: request,
  })
}

export function updateEquipment(
  accessToken: string,
  equipmentId: number,
  request: EquipmentWriteRequest,
) {
  return apiRequest<EquipmentDto>(`/equipments/${equipmentId}`, {
    accessToken,
    method: "PUT",
    json: request,
  })
}

export function deleteEquipment(accessToken: string, equipmentId: number) {
  return apiRequest<void>(`/equipments/${equipmentId}`, {
    accessToken,
    method: "DELETE",
  })
}
