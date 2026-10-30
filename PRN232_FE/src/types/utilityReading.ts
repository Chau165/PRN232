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
  // Chỉ cần cho lần ghi đầu tiên của hợp đồng; các tháng sau BE tự nối tiếp tháng trước.
  electricStart: number | null
  waterStart: number | null
}
