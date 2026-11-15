import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { formatContractDate, isContractDate } from "@/utils/managementContracts"

export const MOVE_OUT_NOTICE_MIN_DAYS = 30

export function addIsoDays(isoDate: string, days: number): string {
  if (!isContractDate(isoDate)) return ""
  const date = new Date(`${isoDate}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

export function diffIsoDays(fromIsoDate: string, toIsoDate: string): number {
  const fromTime = Date.parse(`${fromIsoDate}T00:00:00Z`)
  const toTime = Date.parse(`${toIsoDate}T00:00:00Z`)
  if (Number.isNaN(fromTime) || Number.isNaN(toTime)) return 0
  return Math.round((toTime - fromTime) / 86_400_000)
}

export function getLatestNoticeDateToKeepDeposit(
  targetMoveOutDate: string,
): string {
  return addIsoDays(targetMoveOutDate, -MOVE_OUT_NOTICE_MIN_DAYS)
}

export function validatePlannedMoveOutDate(params: {
  plannedMoveOutDate: string
  endDate: string
  today?: string
}): string | null {
  const value = params.plannedMoveOutDate.trim()
  if (!value || !isContractDate(value)) {
    return "Vui lòng chọn ngày dự kiến trả phòng hợp lệ."
  }

  const today = params.today ?? getVietnamTodayIsoDate()
  if (value < today) {
    return "Ngày dự kiến trả phòng không được ở quá khứ."
  }

  if (params.endDate && isContractDate(params.endDate) && value > params.endDate) {
    return "Ngày dự kiến trả phòng không được sau ngày kết thúc hợp đồng. Muốn ở lâu hơn hãy gia hạn hợp đồng."
  }

  return null
}

export interface MoveOutDepositEvaluation {
  forfeitsDeposit: boolean
  noticeDays: number
  latestNoticeDate: string
  warningMessage: string | null
}

export function evaluateMoveOutDepositWarning(params: {
  plannedMoveOutDate: string
  endDate: string
  today?: string
}): MoveOutDepositEvaluation | null {
  const value = params.plannedMoveOutDate.trim()
  if (!value || !isContractDate(value)) return null

  const today = params.today ?? getVietnamTodayIsoDate()
  if (value < today) return null
  if (params.endDate && isContractDate(params.endDate) && value > params.endDate) {
    return null
  }

  const noticeDays = diffIsoDays(today, value)
  const latestNoticeDate = getLatestNoticeDateToKeepDeposit(value)
  const isBeforeContractEnd = Boolean(
    params.endDate && isContractDate(params.endDate) && value < params.endDate,
  )
  const forfeitsDeposit =
    isBeforeContractEnd && noticeDays < MOVE_OUT_NOTICE_MIN_DAYS

  const warningMessage = forfeitsDeposit
    ? `Bạn trả phòng trước hạn hợp đồng mà báo trước dưới 30 ngày nên sẽ mất phần tiền cọc còn lại khi quyết toán. Báo muộn nhất ngày ${formatContractDate(latestNoticeDate)} để giữ cọc.`
    : null

  return {
    forfeitsDeposit,
    noticeDays,
    latestNoticeDate,
    warningMessage,
  }
}
