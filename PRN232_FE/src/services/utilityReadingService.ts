import { apiRequest } from "@/api/apiClient"
import type { CreateUtilityReadingRequest, UtilityReadingDto } from "@/types/utilityReading"

export function getUtilityReadings(accessToken: string, contractId: number, signal?: AbortSignal) {
  return apiRequest<UtilityReadingDto[]>(`/utility-readings?contractId=${contractId}`, { accessToken, signal, cache: "no-store" })
}

export function createUtilityReading(accessToken: string, request: CreateUtilityReadingRequest) {
  return apiRequest<UtilityReadingDto>("/utility-readings", { accessToken, method: "POST", body: JSON.stringify(request) })
}
