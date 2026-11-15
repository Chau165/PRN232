import { apiRequest } from "@/api/apiClient"
import type {
  ContractOccupantsListDto,
  CreateContractOccupantRequest,
  RecordOccupantMoveOutRequest,
  RevealOccupantIdentityResponseDto,
} from "@/types/contract"

export function getContractOccupants(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<ContractOccupantsListDto>(
    `/contracts/${contractId}/occupants`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function addContractOccupant(
  accessToken: string,
  contractId: number,
  request: CreateContractOccupantRequest,
) {
  return apiRequest<ContractOccupantsListDto>(
    `/contracts/${contractId}/occupants`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify(request),
    },
  )
}

export function recordContractOccupantMoveOut(
  accessToken: string,
  contractId: number,
  occupantId: number,
  request: RecordOccupantMoveOutRequest,
) {
  return apiRequest<ContractOccupantsListDto>(
    `/contracts/${contractId}/occupants/${occupantId}/move-out`,
    {
      accessToken,
      method: "PUT",
      body: JSON.stringify(request),
    },
  )
}

export function revealContractOccupantIdentity(
  accessToken: string,
  contractId: number,
  occupantId: number,
) {
  return apiRequest<RevealOccupantIdentityResponseDto>(
    `/contracts/${contractId}/occupants/${occupantId}/reveal-identity`,
    {
      accessToken,
      method: "POST",
      cache: "no-store",
    },
  )
}
