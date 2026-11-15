import { apiRequest } from "@/api/apiClient"
import type { ContractDto, SaveMoveOutNoticeRequest } from "@/types/contract"

export function saveContractMoveOutNotice(
  accessToken: string,
  contractId: number,
  request: SaveMoveOutNoticeRequest,
) {
  return apiRequest<ContractDto>(`/contracts/${contractId}/move-out-notice`, {
    accessToken,
    method: "PUT",
    body: JSON.stringify(request),
  })
}

export function cancelContractMoveOutNotice(
  accessToken: string,
  contractId: number,
) {
  return apiRequest<ContractDto>(`/contracts/${contractId}/move-out-notice`, {
    accessToken,
    method: "DELETE",
  })
}
