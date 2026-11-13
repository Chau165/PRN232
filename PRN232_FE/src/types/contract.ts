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
  prepaidRentAmount: number
  prepaidRentApplied: number
  remainingPrepaidCredit: number
  propertyAddress: string | null
  lessorUserId: number | null
  lessorName: string | null
  lessorAddress: string | null
  displayStatus: "Upcoming" | "Active" | "ExpiredPendingCheckout" | "Terminated" | "Ended" | string
  initialElectricReading?: number | null
  initialWaterReading?: number | null
  meterHandoverDate?: string | null
  hasMeterHandover?: boolean
  hasUtilityReadings?: boolean
  hasInvoices?: boolean
}

export interface ContractMeterHandoverValues {
  meterHandoverDate: string
  initialElectricReading: number
  initialWaterReading: number
}

export interface SaveContractMeterHandoverRequest extends ContractMeterHandoverValues {
  expectedMeterHandover?: ContractMeterHandoverValues | null
}

export interface TerminateContractRequestDto {
  checkoutConfirmed: boolean
  roomReadyForRental: boolean
  outstandingDebtAcknowledged: boolean
}

export interface TerminateContractResponseDto {
  contract: ContractDto
  outstandingDebt: number
}

export interface PlannedBillingPeriodDto {
  invoiceMonth: string
  monthlyRent: number
  displayStatus: "NotIssued" | string
  grossTotal: number | null
  prepaidRentApplied: number | null
  netPayable: number | null
  utilitiesStatus: string
  note: string | null
}

export interface ContractBillingTimelineDto {
  contract: ContractDto
  issuedInvoices: import("@/types/invoice").InvoiceDto[]
  plannedBillingPeriods: PlannedBillingPeriodDto[]
  hasMorePlannedPeriods: boolean
}
