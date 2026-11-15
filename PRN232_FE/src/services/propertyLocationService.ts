import { apiRequest } from "@/api/apiClient"
export type LocationAddress = {
  addressText: string
  provinceCode: string
  wardCode: string
}
export type LocationCandidate = {
  location: {
    displayAddress: string
    precision: string
    provider: string
    coordinates: { latitude: number; longitude: number }
  }
  matchLevel: string
  reasons: string[]
}
export type LocationPreview = {
  status: string
  candidates: LocationCandidate[]
  providers: { provider: string; status: string }[]
}
export function previewPropertyLocation(
  accessToken: string,
  address: LocationAddress,
  signal?: AbortSignal,
) {
  return apiRequest<LocationPreview>("/properties/location-preview", {
    accessToken,
    method: "POST",
    json: address,
    signal,
  })
}
