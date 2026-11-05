import { apiRequest } from "@/api/apiClient"
import type { PropertyDto } from "@/types/user"
import type { PropertyManagerAssignmentDto } from "@/types/propertyManagerAssignment"

export function getPropertyManagerAssignments(
  accessToken: string,
  propertyId: number,
  signal?: AbortSignal,
) {
  return apiRequest<PropertyManagerAssignmentDto[]>(
    `/properties/${propertyId}/managers`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function assignPropertyManager(
  accessToken: string,
  propertyId: number,
  managerId: number,
) {
  return apiRequest<PropertyManagerAssignmentDto>(`/properties/${propertyId}/managers`, {
    accessToken,
    method: "POST",
    json: { managerId },
  })
}

export function revokePropertyManager(
  accessToken: string,
  propertyId: number,
  managerId: number,
) {
  return apiRequest<void>(`/properties/${propertyId}/managers/${managerId}`, {
    accessToken,
    method: "DELETE",
  })
}

export function getPropertiesForManager(
  accessToken: string,
  managerId: number,
  signal?: AbortSignal,
) {
  return apiRequest<PropertyDto[]>(`/managers/${managerId}/properties`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getMyManagedProperties(accessToken: string, signal?: AbortSignal) {
  return apiRequest<PropertyDto[]>("/properties/my-managed", {
    accessToken,
    signal,
    cache: "no-store",
  })
}
