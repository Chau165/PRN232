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
  securityDepositAmount?: number
  registeredVehicleCount?: number | null
  manualSecurityDepositAmount?: number | null
  securityDepositAcknowledgedAtUtc?: string | null
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
  plannedMoveOutDate?: string | null
  moveOutNoticeAtUtc?: string | null
  moveOutNoticeDays?: number | null
  moveOutForfeitsDeposit?: boolean
  hasDepositSettlement?: boolean
  isDepositSettled?: boolean
  transferredFromContractId?: number | null
  transferredToContractId?: number | null
}

export interface SaveMoveOutNoticeRequest {
  plannedMoveOutDate: string
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

export type HandoverKind = "MoveIn" | "MoveOut"
export type EquipmentCondition = "Good" | "Worn" | "Damaged" | "Missing"

export interface HandoverItemDto {
  handoverItemId: number
  equipmentId: number
  equipmentName: string
  quantity: number
  compensationValue: number
  condition: EquipmentCondition | string
  chargeAmount: number
  note: string | null
  moveInCondition: EquipmentCondition | string | null
}

export interface HandoverRecordDto {
  handoverRecordId: number
  kind: HandoverKind | string
  handoverDate: string
  note: string | null
  recordedByName: string | null
  recordedAtUtc: string
  totalCharge: number
  items: HandoverItemDto[]
}

export interface HandoverRoomEquipmentDto {
  equipmentId: number
  name: string
  quantity: number
  compensationValue: number
}

export interface ContractHandoversDto {
  contractId: number
  moveIn: HandoverRecordDto | null
  moveOut: HandoverRecordDto | null
  roomEquipments: HandoverRoomEquipmentDto[]
}

export interface SaveHandoverItemRequest {
  equipmentId: number
  condition: EquipmentCondition
  chargeAmount: number
  note?: string | null
}

export interface SaveHandoverRecordRequest {
  handoverDate: string
  note?: string | null
  items: SaveHandoverItemRequest[]
}

export type ContractEvidenceKind = "MoveIn" | "MoveOut" | "SignedContract"

export interface ContractEvidenceFileDto {
  contractEvidenceFileId: number
  kind: ContractEvidenceKind | string
  fileName: string
  contentType: string
  sizeBytes: number
  uploadedByName: string
  uploadedByRole: string
  uploadedAtUtc: string
  canDelete: boolean
  url: string | null
  isHidden: boolean
  hiddenReason: string | null
}

export interface ContractEvidenceListDto {
  contractId: number
  moveInUploadDeadline: string | null
  canUploadMoveIn: boolean
  canUploadMoveOut: boolean
  canUploadSignedContract: boolean
  files: ContractEvidenceFileDto[]
}

export interface DepositSettlementDto {
  contractId: number
  isSettled: boolean
  moveOutDate: string
  contractEndDate: string
  depositAmount: number
  damageCharges: number
  damageCoveredByDeposit: number
  outstandingDebt: number
  retainedForDebt: number
  forfeitedAmount: number
  refundAmount: number
  amountDue: number
  earlyWithoutNotice: boolean
  noticeDays: number | null
  note: string | null
  settledByName: string | null
  settledAtUtc: string | null
  refundConfirmedAtUtc: string | null
  refundConfirmedByName: string | null
  dueCollectedAtUtc: string | null
  dueCollectedByName: string | null
}

export interface ConfirmDepositSettlementRequest {
  outstandingDebtAcknowledged: boolean
  note?: string | null
}

export interface ContractOccupantDto {
  contractOccupantId: number
  fullName: string
  phoneNumber: string | null
  maskedIdentityNumber: string
  moveInDate: string
  moveOutDate: string | null
  isActive: boolean
  note: string | null
  addedByName: string | null
  createdAtUtc: string
}

export interface ContractOccupantsListDto {
  contractId: number
  holderName: string
  maxOccupants: number
  currentCount: number
  canManage: boolean
  canRevealIdentity: boolean
  occupants: ContractOccupantDto[]
}

export interface CreateContractOccupantRequest {
  fullName: string
  phoneNumber?: string | null
  identityNumber: string
  moveInDate: string
  note?: string | null
}

export interface RecordOccupantMoveOutRequest {
  moveOutDate: string
}

export interface RevealOccupantIdentityResponseDto {
  contractOccupantId: number
  fullName: string
  identityNumber: string
}

export type ContractTransferStatus = "Pending" | "Completed" | "Cancelled"

export interface ContractTransferDto {
  contractTransferId: number
  status: ContractTransferStatus | string
  fromContractId: number
  roomCode: string
  propertyName: string
  oldHolderId: number
  oldHolderName: string
  newHolderId: number
  newHolderName: string
  newHolderEmail: string
  becomingHolderOccupantName: string | null
  effectiveDate: string
  monthlyRent: number
  contractEndDate: string
  depositCarriedOver: number
  requestedByName: string
  createdAtUtc: string
  oldHolderConfirmedAtUtc: string | null
  newHolderConfirmedAtUtc: string | null
  completedAtUtc: string | null
  newContractId: number | null
  cancelledAtUtc: string | null
  cancelReason: string | null
  canConfirm: boolean
  canCancel: boolean
  canComplete: boolean
  blockers: string[]
}

export interface CreateContractTransferRequest {
  contractId: number
  newHolderEmail: string
  effectiveDate: string
  occupantId?: number | null
}

export interface CancelContractTransferRequest {
  reason?: string | null
}
