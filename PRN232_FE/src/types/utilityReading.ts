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
  warnings?: string[] | null
}

export interface UtilityReadingPreviewDto {
  contractId: number
  readingMonth: string
  electricStart: number
  electricEnd: number
  electricUsage: number
  waterStart: number
  waterEnd: number
  waterUsage: number
  averageElectricUsage: number | null
  averageWaterUsage: number | null
  monthsAveraged: number
  warnings: string[]
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
