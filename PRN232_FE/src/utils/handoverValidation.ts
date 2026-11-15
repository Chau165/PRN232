// Quy tắc hiển thị & validate biên bản bàn giao thiết bị (C14 — docs/rebuild/API-CHANGES.md).

import type { EquipmentCondition, HandoverKind } from "@/types/contract"
import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"

export const EQUIPMENT_CONDITIONS: readonly {
  value: EquipmentCondition
  label: string
}[] = [
  { value: "Good", label: "Tốt" },
  { value: "Worn", label: "Hao mòn" },
  { value: "Damaged", label: "Hư hỏng" },
  { value: "Missing", label: "Mất" },
]

export function formatConditionLabel(
  condition: EquipmentCondition | string | null | undefined,
): string {
  switch (condition) {
    case "Good":
      return "Tốt"
    case "Worn":
      return "Hao mòn"
    case "Damaged":
      return "Hư hỏng"
    case "Missing":
      return "Mất"
    default:
      return condition || "Chưa ghi nhận"
  }
}

export function isDamageOrMissingCondition(
  condition: EquipmentCondition | string | null | undefined,
): boolean {
  return condition === "Damaged" || condition === "Missing"
}

export function canChargeForHandoverItem(
  kind: HandoverKind,
  condition: EquipmentCondition | string | null | undefined,
  moveInCondition?: EquipmentCondition | string | null,
): boolean {
  if (kind !== "MoveOut") return false
  if (!isDamageOrMissingCondition(condition)) return false
  if (isDamageOrMissingCondition(moveInCondition)) return false
  return true
}

export function handoverDateError(
  handoverDate: string | null | undefined,
  today: string = getVietnamTodayIsoDate(),
): string | null {
  const trimmed = handoverDate?.trim() ?? ""
  if (!trimmed) {
    return "Vui lòng chọn ngày bàn giao."
  }
  if (trimmed > today) {
    return "Ngày bàn giao không được ở tương lai."
  }
  return null
}

export function handoverConditionError(
  condition: string | null | undefined,
): string | null {
  if (
    condition !== "Good" &&
    condition !== "Worn" &&
    condition !== "Damaged" &&
    condition !== "Missing"
  ) {
    return "Tình trạng phải là Tốt, Hao mòn, Hư hỏng hoặc Mất."
  }
  return null
}

export function handoverChargeAmountError(params: {
  kind: HandoverKind
  equipmentName: string
  quantity: number
  compensationValue: number
  condition: EquipmentCondition | string
  moveInCondition?: EquipmentCondition | string | null
  chargeAmount: number | string | null | undefined
}): string | null {
  const {
    kind,
    equipmentName,
    quantity,
    compensationValue,
    condition,
    moveInCondition,
    chargeAmount,
  } = params

  if (
    chargeAmount === null ||
    chargeAmount === undefined ||
    (typeof chargeAmount === "string" && chargeAmount.trim() === "")
  ) {
    return null
  }

  const value =
    typeof chargeAmount === "number" ? chargeAmount : Number(chargeAmount)
  if (!Number.isFinite(value) || value < 0) {
    return "Tiền bồi thường không được âm."
  }
  if (!Number.isInteger(value) || value % 1_000 !== 0) {
    return "Tiền bồi thường phải là bội số của 1.000đ."
  }
  if (value === 0) {
    return null
  }

  if (kind === "MoveIn") {
    return "Biên bản nhận phòng không tính tiền bồi thường."
  }
  if (!isDamageOrMissingCondition(condition)) {
    return `${equipmentName}: chỉ tính tiền khi thiết bị hư hỏng hoặc mất (hao mòn tự nhiên không phải bồi thường).`
  }
  if (isDamageOrMissingCondition(moveInCondition)) {
    return `${equipmentName} đã hư hỏng hoặc mất từ lúc nhận phòng nên không tính tiền khách.`
  }

  const maxCharge = quantity * compensationValue
  if (compensationValue > 0 && value > maxCharge) {
    return `${equipmentName}: tiền bồi thường tối đa ${maxCharge.toLocaleString("vi-VN")}đ (${quantity} × ${compensationValue.toLocaleString("vi-VN")}đ).`
  }

  return null
}

export function splitHandoverValidationErrors(
  validationErrors: Record<string, string[]> | undefined,
  fallbackMessage: string,
): { fieldErrors: Record<string, string>; generalError: string } {
  if (!validationErrors) {
    return { fieldErrors: {}, generalError: fallbackMessage }
  }

  const fieldErrors: Record<string, string> = {}
  const unmappedMessages: string[] = []

  for (const [rawKey, rawMessages] of Object.entries(validationErrors)) {
    const messages = rawMessages.map((msg) => msg.trim()).filter(Boolean)
    if (messages.length === 0) continue

    const joined = messages.join(" ")
    const normalized = rawKey.trim()

    if (/^handoverdate$/i.test(normalized)) {
      fieldErrors.handoverDate = joined
      continue
    }
    if (/^note$/i.test(normalized)) {
      fieldErrors.note = joined
      continue
    }

    const itemMatch = /^items\[(\d+)\]\.(condition|chargeamount|note)$/i.exec(
      normalized,
    )
    if (itemMatch) {
      const idx = itemMatch[1]
      const propRaw = itemMatch[2].toLowerCase()
      const prop =
        propRaw === "chargeamount"
          ? "chargeAmount"
          : propRaw === "condition"
            ? "condition"
            : "note"
      fieldErrors[`items[${idx}].${prop}`] = joined
      continue
    }

    unmappedMessages.push(...messages)
  }

  const hasFieldErrors = Object.keys(fieldErrors).length > 0
  if (!hasFieldErrors) {
    return {
      fieldErrors,
      generalError: fallbackMessage || unmappedMessages.join(" "),
    }
  }

  return {
    fieldErrors,
    generalError: unmappedMessages.join(" "),
  }
}
