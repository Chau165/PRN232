import { apiRequest } from "@/api/apiClient"
import type {
  CancelContractTransferRequest,
  ContractTransferDto,
  CreateContractTransferRequest,
} from "@/types/contract"

export function getContractTransfers(
  accessToken: string,
  signal?: AbortSignal,
) {
  return apiRequest<ContractTransferDto[]>("/contract-transfers", {
    accessToken,
    signal,
    cache: "no-store",
  })
}

export function createContractTransfer(
  accessToken: string,
  request: CreateContractTransferRequest,
) {
  return apiRequest<ContractTransferDto>("/contract-transfers", {
    accessToken,
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function confirmContractTransfer(
  accessToken: string,
  transferId: number,
) {
  return apiRequest<ContractTransferDto>(
    `/contract-transfers/${transferId}/confirm`,
    {
      accessToken,
      method: "POST",
    },
  )
}

export function cancelContractTransfer(
  accessToken: string,
  transferId: number,
  request: CancelContractTransferRequest = {},
) {
  return apiRequest<ContractTransferDto>(
    `/contract-transfers/${transferId}/cancel`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify(request),
    },
  )
}

export function completeContractTransfer(
  accessToken: string,
  transferId: number,
) {
  return apiRequest<ContractTransferDto>(
    `/contract-transfers/${transferId}/complete`,
    {
      accessToken,
      method: "POST",
    },
  )
}
