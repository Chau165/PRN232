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
