import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { isContractDate } from "@/utils/managementContracts"
import { addIsoDays } from "@/utils/moveOutNoticeValidation"

export const REVEAL_IDENTITY_TIMEOUT_MS = 30_000

export function occupantFullNameError(value: string): string | null {
  const trimmed = value.trim()
  if (!trimmed) {
    return "Vui lòng nhập họ tên người ở cùng."
  }
  if (trimmed.length < 2 || trimmed.length > 100) {
    return "Họ tên phải từ 2 đến 100 ký tự."
  }
  return null
}

export function occupantPhoneNumberError(value: string): string | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  if (!/^0\d{9}$/.test(trimmed)) {
    return "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0."
  }
  return null
}

export function occupantIdentityNumberError(value: string): string | null {
  const trimmed = value.trim()
  if (!/^\d{12}$/.test(trimmed)) {
    return "Số CCCD phải gồm đúng 12 chữ số."
  }
  return null
}

export function occupantMoveInDateError(params: {
  moveInDate: string
  contractStartDate?: string
  contractEndDate?: string
  today?: string
}): string | null {
  const value = params.moveInDate.trim()
  if (!value || !isContractDate(value)) {
    return "Vui lòng chọn ngày dọn vào hợp lệ."
  }

  if (
    params.contractStartDate &&
    isContractDate(params.contractStartDate) &&
    value < params.contractStartDate
  ) {
    return "Ngày dọn vào không được trước ngày bắt đầu hợp đồng."
  }

  if (
    params.contractEndDate &&
    isContractDate(params.contractEndDate) &&
    value > params.contractEndDate
  ) {
    return "Ngày dọn vào không được sau ngày kết thúc hợp đồng."
  }

  const today = params.today ?? getVietnamTodayIsoDate()
  const maxFutureDate = addIsoDays(today, 14)
  if (maxFutureDate && value > maxFutureDate) {
    return "Ngày dọn vào không được quá 14 ngày tới."
  }

  return null
}

export function occupantMoveOutDateError(params: {
  moveOutDate: string
  moveInDate: string
  contractEndDate?: string
  today?: string
}): string | null {
  const value = params.moveOutDate.trim()
  if (!value || !isContractDate(value)) {
    return "Vui lòng chọn ngày dọn ra hợp lệ."
  }

  if (params.moveInDate && isContractDate(params.moveInDate) && value < params.moveInDate) {
    return "Ngày dọn ra không được trước ngày dọn vào."
  }

  if (
    params.contractEndDate &&
    isContractDate(params.contractEndDate) &&
    value > params.contractEndDate
  ) {
    return "Ngày dọn ra không được sau ngày kết thúc hợp đồng."
  }

  const today = params.today ?? getVietnamTodayIsoDate()
  if (value > today) {
    return "Ngày dọn ra không được ở tương lai."
  }

  return null
}
