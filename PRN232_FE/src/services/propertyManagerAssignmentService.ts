import { apiRequest } from "@/api/apiClient"
import type { PropertyDto } from "@/types/user"
import type {
  PropertyManagerAssignmentDto,
  PropertyManagersDto,
} from "@/types/propertyManagerAssignment"

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

/**
 * Loads manager assignments for many properties with a single request (C11).
 * Omitting `propertyIds` returns assignments for every property.
 * Returns a map keyed by propertyId; properties without managers map to [].
 */
export async function getManagersForProperties(
  accessToken: string,
  propertyIds?: readonly number[],
  signal?: AbortSignal,
): Promise<Record<string, PropertyManagerAssignmentDto[]>> {
  const result: Record<string, PropertyManagerAssignmentDto[]> = {}
  if (propertyIds && propertyIds.length === 0) return result

  const query = propertyIds
    ? `?propertyIds=${encodeURIComponent(propertyIds.join(","))}`
    : ""
  const groups = await apiRequest<PropertyManagersDto[]>(
    `/properties/managers${query}`,
    { accessToken, signal, cache: "no-store" },
  )
  for (const id of propertyIds ?? []) result[String(id)] = []
  for (const group of groups ?? []) result[String(group.propertyId)] = group.managers
  return result
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
