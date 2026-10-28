import { apiRequest } from "@/api/apiClient"

export interface ContractDto {
  contractId: number
  bookingId: number | null
  roomId: number
  roomCode: string
  propertyId: number
  propertyName: string
  customerId: number
  customerName: string
  customerPhone: string
  // Ngày theo lịch, dạng "yyyy-MM-dd" (DateOnly bên BE).
  startDate: string
  endDate: string
  monthlyRent: number
  depositAmount: number
  status: string
  processedByStaffName: string | null
}

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
