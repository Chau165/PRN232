import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { isContractDate } from "@/utils/managementContracts"
import { addIsoDays } from "@/utils/moveOutNoticeValidation"

export const MAX_TRANSFER_CANCEL_REASON_LENGTH = 500

export function getAvailableTransferEffectiveDates(params: {
  contractEndDate: string
  contractStartDate?: string
  today?: string
}): string[] {
  if (!isContractDate(params.contractEndDate)) return []

  const today = params.today ?? getVietnamTodayIsoDate()
  const currentMonthFirst = `${today.slice(0, 7)}-01`

  const startDateObj = new Date(`${currentMonthFirst}T00:00:00Z`)
  const results: string[] = []

  // Giới hạn tối đa 120 tháng để tránh vòng lặp vô hạn nếu hợp đồng dài bất thường.
  for (let i = 0; i < 120; i++) {
    const candidate = startDateObj.toISOString().slice(0, 10)
    if (candidate >= params.contractEndDate) break
    if (!params.contractStartDate || candidate >= params.contractStartDate) {
      results.push(candidate)
    }
    startDateObj.setUTCMonth(startDateObj.getUTCMonth() + 1)
  }

  return results
}

export function transferEmailError(value: string): string | null {
  const trimmed = value.trim()
  if (!trimmed) {
    return "Vui lòng nhập email tài khoản khách nhận hợp đồng."
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
    return "Email người nhận không đúng định dạng."
  }
  return null
}

export function transferEffectiveDateError(params: {
  effectiveDate: string
  contractEndDate: string
  contractStartDate?: string
  today?: string
}): string | null {
  const value = params.effectiveDate.trim()
  if (!value || !isContractDate(value)) {
    return "Vui lòng chọn ngày hiệu lực hợp lệ."
  }
  if (!value.endsWith("-01")) {
    return "Ngày hiệu lực phải là ngày 1 của một tháng (điện nước và hóa đơn chốt theo tháng)."
  }

  const today = params.today ?? getVietnamTodayIsoDate()
  const currentMonthFirst = `${today.slice(0, 7)}-01`
  if (value < currentMonthFirst) {
    return "Ngày hiệu lực phải từ tháng hiện tại trở đi."
  }

  if (
    params.contractStartDate &&
    isContractDate(params.contractStartDate) &&
    value < params.contractStartDate
  ) {
    return "Ngày hiệu lực không được trước ngày bắt đầu hợp đồng."
  }

  if (
    params.contractEndDate &&
    isContractDate(params.contractEndDate) &&
    value >= params.contractEndDate
  ) {
    return "Ngày hiệu lực phải trước ngày kết thúc hợp đồng."
  }

  return null
}

export function transferCancelReasonError(reason: string): string | null {
  if (reason.trim().length > MAX_TRANSFER_CANCEL_REASON_LENGTH) {
    return "Lý do hủy đề nghị không được vượt quá 500 ký tự."
  }
  return null
}

export function getOldContractEndDateOnTransfer(effectiveDate: string): string {
  return addIsoDays(effectiveDate, -1)
}
