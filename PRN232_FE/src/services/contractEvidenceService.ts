import { apiRequest } from "@/api/apiClient"
import type {
  ContractEvidenceKind,
  ContractEvidenceListDto,
} from "@/types/contract"

export function getContractEvidence(
  accessToken: string,
  contractId: number,
  signal?: AbortSignal,
) {
  return apiRequest<ContractEvidenceListDto>(
    `/contracts/${contractId}/evidence`,
    { accessToken, signal, cache: "no-store" },
  )
}

export function uploadContractEvidence(
  accessToken: string,
  contractId: number,
  kind: ContractEvidenceKind,
  file: File,
) {
  const formData = new FormData()
  formData.append("file", file)
  return apiRequest<ContractEvidenceListDto>(
    `/contracts/${contractId}/evidence?kind=${encodeURIComponent(kind)}`,
    {
      accessToken,
      method: "POST",
      body: formData,
    },
  )
}

export function deleteContractEvidence(
  accessToken: string,
  contractId: number,
  fileId: number,
) {
  return apiRequest<ContractEvidenceListDto>(
    `/contracts/${contractId}/evidence/${fileId}`,
    {
      accessToken,
      method: "DELETE",
    },
  )
}

export function hideContractEvidence(
  accessToken: string,
  contractId: number,
  fileId: number,
  reason: string,
) {
  return apiRequest<ContractEvidenceListDto>(
    `/contracts/${contractId}/evidence/${fileId}/hide`,
    {
      accessToken,
      method: "POST",
      body: JSON.stringify({ reason }),
    },
  )
}
