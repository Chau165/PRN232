import { apiRequest } from "@/api/apiClient"
import type {
  ContractHandoversDto,
  SaveHandoverRecordRequest,
} from "@/types/contract"

export function getContractHandovers(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<ContractHandoversDto>(
    `/contracts/${contractId}/handovers`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function saveContractMoveInHandover(
  accessToken: string,
  contractId: number,
  request: SaveHandoverRecordRequest,
) {
  return apiRequest<ContractHandoversDto>(
    `/contracts/${contractId}/handovers/move-in`,
    {
      accessToken,
      method: "PUT",
      body: JSON.stringify(request),
    },
  )
}

export function saveContractMoveOutHandover(
  accessToken: string,
  contractId: number,
  request: SaveHandoverRecordRequest,
) {
  return apiRequest<ContractHandoversDto>(
    `/contracts/${contractId}/handovers/move-out`,
    {
      accessToken,
      method: "PUT",
      body: JSON.stringify(request),
    },
  )
}
