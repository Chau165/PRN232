import { formatVnd } from "@/utils/publicFormatting"

export function requiresDepositSettlement(contract: {
  securityDepositAmount?: number | null
}): boolean {
  return (contract.securityDepositAmount ?? 0) > 0
}

export function canTerminateContractWithSettlement(
  contract: {
    securityDepositAmount?: number | null
    isDepositSettled?: boolean
    hasDepositSettlement?: boolean
  },
  isSettledOverride?: boolean,
): boolean {
  if (!requiresDepositSettlement(contract)) {
    return true
  }
  return Boolean(
    isSettledOverride ??
      contract.isDepositSettled ??
      contract.hasDepositSettlement,
  )
}

export function formatForfeitedDepositReason(settlement: {
  earlyWithoutNotice: boolean
  noticeDays: number | null
  forfeitedAmount: number
}): string | null {
  if (!settlement.earlyWithoutNotice && settlement.forfeitedAmount <= 0) {
    return null
  }
  return `trả trước hạn, báo trước ${settlement.noticeDays ?? 0} ngày < 30`
}

export function validateDepositSettlementConfirmation(params: {
  outstandingDebt: number
  outstandingDebtAcknowledged: boolean
}): string | null {
  if (params.outstandingDebt > 0 && !params.outstandingDebtAcknowledged) {
    return `Khách còn nợ hóa đơn ${formatVnd(params.outstandingDebt)}. Nên thu hết trước khi quyết toán; nếu khách không trả, hãy xác nhận để giữ lại cọc bù nợ.`
  }
  return null
}

export function canConfirmSettlementPayment(
  user: { id?: string; role?: string } | null | undefined,
  contract: { lessorUserId?: number | null },
): boolean {
  const role = user?.role?.toUpperCase()
  if (role === "MANAGER") return true
  if (role === "ADMIN") {
    return (
      contract.lessorUserId != null &&
      user?.id != null &&
      String(contract.lessorUserId) === String(user.id)
    )
  }
  return false
}

export function canConfirmSettlementRefund(settlement: {
  isSettled: boolean
  refundAmount: number
  refundConfirmedAtUtc: string | null
}): boolean {
  return (
    settlement.isSettled &&
    settlement.refundAmount > 0 &&
    !settlement.refundConfirmedAtUtc
  )
}

export function canConfirmSettlementDueCollected(settlement: {
  isSettled: boolean
  amountDue: number
  dueCollectedAtUtc: string | null
}): boolean {
  return (
    settlement.isSettled &&
    settlement.amountDue > 0 &&
    !settlement.dueCollectedAtUtc
  )
}
