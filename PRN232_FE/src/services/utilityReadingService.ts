import { apiRequest } from "@/api/apiClient"
import type {
  CreateUtilityReadingRequest,
  UtilityReadingDto,
  UtilityReadingPreviewDto,
} from "@/types/utilityReading"

export function getUtilityReadings(accessToken: string, contractId: number, signal?: AbortSignal) {
  return apiRequest<UtilityReadingDto[]>(`/utility-readings?contractId=${contractId}`, { accessToken, signal, cache: "no-store" })
}

export function previewUtilityReading(accessToken: string, request: CreateUtilityReadingRequest) {
  return apiRequest<UtilityReadingPreviewDto>("/utility-readings/preview", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function createUtilityReading(accessToken: string, request: CreateUtilityReadingRequest) {
  return apiRequest<UtilityReadingDto>("/utility-readings", { accessToken, method: "POST", body: JSON.stringify(request) })
}
