import { apiRequest } from "@/api/apiClient"
import type { ContractDto } from "@/types/contract"

export function getContracts(accessToken: string, signal?: AbortSignal) {
  return apiRequest<ContractDto[]>("/contracts", { accessToken, signal, cache: "no-store" })
}

export function createContract(
  accessToken: string,
  request: { bookingId: number; startDate: string; endDate: string },
) {
  return apiRequest<ContractDto>("/contracts", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}
