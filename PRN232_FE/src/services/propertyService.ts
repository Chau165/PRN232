import { apiRequest } from "@/api/apiClient"
import type {
  AdministrativeProvinceOption,
  AdministrativeWardOption,
} from "@/types/administrativeDivision"
import type { PropertyDto } from "@/types/user"

export type PropertyWriteRequest = {
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  provinceCode?: string
  wardCode?: string
  primaryImage?: File
  description?: string
}

function toPropertyFormData(request: PropertyWriteRequest) {
  const formData = new FormData()
  formData.append("propertyName", request.propertyName)
  formData.append("addressText", request.addressText)
  formData.append("ward", request.ward)
  formData.append("district", request.district)
  formData.append("city", request.city)
  if (request.provinceCode) formData.append("provinceCode", request.provinceCode)
  if (request.wardCode) formData.append("wardCode", request.wardCode)
  if (request.description) formData.append("description", request.description)
  if (request.primaryImage) {
    formData.append("primaryImage", request.primaryImage, request.primaryImage.name)
  }
  return formData
}

export function getAdministrativeProvinces(
  accessToken: string,
  signal?: AbortSignal,
) {
  return apiRequest<AdministrativeProvinceOption[]>("/locations/provinces", {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getAdministrativeWards(
  accessToken: string,
  provinceCode: string,
  signal?: AbortSignal,
) {
  return apiRequest<AdministrativeWardOption[]>(
    `/locations/wards?provinceCode=${encodeURIComponent(provinceCode)}`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function getProperties(accessToken: string, signal?: AbortSignal) {
  return apiRequest<PropertyDto[]>("/properties", {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function getProperty(
  accessToken: string,
  propertyId: number,
  signal?: AbortSignal,
) {
  return apiRequest<PropertyDto>(`/properties/${propertyId}`, {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function createProperty(
  accessToken: string,
  request: PropertyWriteRequest,
) {
  return apiRequest<PropertyDto>("/properties", {
    accessToken,
    method: "POST",
    body: toPropertyFormData(request),
  })
}

export function updateProperty(
  accessToken: string,
  propertyId: number,
  request: PropertyWriteRequest,
) {
  return apiRequest<PropertyDto>(`/properties/${propertyId}`, {
    accessToken,
    method: "PUT",
    body: toPropertyFormData(request),
  })
}

export function deleteProperty(accessToken: string, propertyId: number) {
  return apiRequest<void>(`/properties/${propertyId}`, {
    accessToken,
    method: "DELETE",
  })
}
