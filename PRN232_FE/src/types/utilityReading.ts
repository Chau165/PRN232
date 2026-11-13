export interface UtilityReadingDto {
  readingId: number
  contractId: number
  roomCode: string
  propertyName: string
  customerName: string
  // Ngày đầu tháng, dạng "yyyy-MM-01".
  readingMonth: string
  electricStart: number
  electricEnd: number
  electricUsage: number
  waterStart: number
  waterEnd: number
  waterUsage: number
}

export interface CreateUtilityReadingRequest {
  contractId: number
  readingMonth: string
  electricEnd: number
  waterEnd: number
  // Legacy compatibility only; the server derives starting values.
  electricStart?: number | null
  waterStart?: number | null
}
