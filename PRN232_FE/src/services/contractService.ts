import { apiRequest } from "@/api/apiClient"
import type {
  ContractBillingTimelineDto,
  ContractDto,
  SaveContractMeterHandoverRequest,
  TerminateContractRequestDto,
  TerminateContractResponseDto,
} from "@/types/contract"

export function getContract(accessToken: string, contractId: number, signal?: AbortSignal) {
  return apiRequest<ContractDto>(`/contracts/${contractId}`, { accessToken, signal, cache: "no-store" })
}

export function saveContractMeterHandover(
  accessToken: string, contractId: number, request: SaveContractMeterHandoverRequest,
) {
  return apiRequest<ContractDto>(`/contracts/${contractId}/meter-handover`, {
    accessToken, method: "PUT", body: JSON.stringify(request),
  })
}

export function getContracts(accessToken: string, signal?: AbortSignal) {
  return apiRequest<ContractDto[]>("/contracts", { accessToken, signal, cache: "no-store" })
}

export function getContractBillingTimeline(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<ContractBillingTimelineDto>(
    `/contracts/${contractId}/billing-timeline`,
    { accessToken, signal, cache: "no-store" },
  )
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

export function extendContract(
  accessToken: string,
  contractId: number,
  request: { newEndDate: string },
  signal?: AbortSignal,
) {
  return apiRequest<ContractDto>(`/contracts/${contractId}/extend`, {
    accessToken,
    method: "PUT",
    body: JSON.stringify(request),
    signal,
  })
}

export function terminateContract(
  accessToken: string,
  contractId: number,
  request: TerminateContractRequestDto,
  signal?: AbortSignal,
) {
  return apiRequest<TerminateContractResponseDto>(`/contracts/${contractId}/terminate`, {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
    signal,
  })
}

export function markTerminatedRoomReady(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<void>(`/contracts/${contractId}/room-ready`, {
    accessToken,
    method: "POST",
    signal,
  })
}
