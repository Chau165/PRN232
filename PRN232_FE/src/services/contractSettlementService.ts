import { apiRequest } from "@/api/apiClient"
import type {
  ConfirmDepositSettlementRequest,
  DepositSettlementDto,
} from "@/types/contract"

export function getContractDepositSettlement(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<DepositSettlementDto>(
    `/contracts/${contractId}/settlement`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function confirmContractDepositSettlement(
  accessToken: string,
  contractId: number,
  request: ConfirmDepositSettlementRequest,
) {
  return apiRequest<DepositSettlementDto>(
    `/contracts/${contractId}/settlement`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify(request),
    },
  )
}
