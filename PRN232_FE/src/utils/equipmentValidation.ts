// Quy tắc validate thiết bị (C3, C7 — docs/rebuild/API-CHANGES.md).

export const EQUIPMENT_NAME_MAX_LENGTH = 200
export const EQUIPMENT_SUPPLIER_MAX_LENGTH = 200
export const EQUIPMENT_QUANTITY_MIN = 1
export const EQUIPMENT_QUANTITY_MAX = 20
export const EQUIPMENT_COMPENSATION_MIN = 0
export const EQUIPMENT_COMPENSATION_MAX = 50_000_000
export const EQUIPMENT_COMPENSATION_STEP = 1_000

export type EquipmentFormField =
  | "roomId"
  | "name"
  | "quantity"
  | "compensationValue"
  | "installDate"
  | "warrantyExpiry"
  | "supplier"
  | "status"

export function getVietnamTodayIsoDate(now: Date = new Date()): string {
  const vnTime = new Date(now.getTime() + 7 * 60 * 60 * 1000)
  return vnTime.toISOString().slice(0, 10)
}

export function equipmentRoomIdError(roomId: number | string): string | null {
  if (typeof roomId === "string" && roomId.trim() === "") {
    return "Vui lòng chọn phòng."
  }
  const id = typeof roomId === "number" ? roomId : Number(roomId)
  if (!Number.isInteger(id) || id < 1) {
    return "Vui lòng chọn phòng."
  }
  return null
}

export function equipmentNameError(name: string): string | null {
  const trimmed = name.trim()
  if (!trimmed || trimmed.length > EQUIPMENT_NAME_MAX_LENGTH) {
    return `Tên thiết bị là bắt buộc và không quá ${EQUIPMENT_NAME_MAX_LENGTH} ký tự.`
  }
  return null
}

export function equipmentSupplierError(supplier: string): string | null {
  const trimmed = supplier.trim()
  if (!trimmed || trimmed.length > EQUIPMENT_SUPPLIER_MAX_LENGTH) {
    return `Nhà cung cấp là bắt buộc và không quá ${EQUIPMENT_SUPPLIER_MAX_LENGTH} ký tự.`
  }
  return null
}

export function installDateError(
  installDate: string,
  today: string = getVietnamTodayIsoDate(),
): string | null {
  const trimmed = installDate.trim()
  if (!trimmed) {
    return "Vui lòng chọn ngày lắp đặt."
  }
  if (trimmed > today) {
    return "Ngày lắp đặt không được ở tương lai."
  }
  return null
}

export function warrantyExpiryError(
  installDate: string,
  warrantyExpiry: string,
): string | null {
  const trimmedWarranty = warrantyExpiry.trim()
  if (!trimmedWarranty) {
    return "Vui lòng chọn hạn bảo hành."
  }
  const trimmedInstall = installDate.trim()
  if (trimmedInstall && trimmedWarranty < trimmedInstall) {
    return "Hạn bảo hành không được trước ngày lắp đặt."
  }
  return null
}

export function equipmentStatusError(status: string): string | null {
  if (status !== "Operational") {
    return "Trạng thái thiết bị không được hỗ trợ."
  }
  return null
}

export function equipmentQuantityError(
  quantity: number | string | null | undefined,
): string | null {
  if (
    quantity === null ||
    quantity === undefined ||
    (typeof quantity === "string" && quantity.trim() === "")
  ) {
    return null
  }
  const value = typeof quantity === "number" ? quantity : Number(quantity)
  if (
    !Number.isInteger(value) ||
    value < EQUIPMENT_QUANTITY_MIN ||
    value > EQUIPMENT_QUANTITY_MAX
  ) {
    return "Số lượng phải từ 1 đến 20."
  }
  return null
}

export function equipmentCompensationValueError(
  compensationValue: number | string | null | undefined,
): string | null {
  if (
    compensationValue === null ||
    compensationValue === undefined ||
    (typeof compensationValue === "string" && compensationValue.trim() === "")
  ) {
    return null
  }
  const value =
    typeof compensationValue === "number"
      ? compensationValue
      : Number(compensationValue)
  if (
    !Number.isFinite(value) ||
    value < EQUIPMENT_COMPENSATION_MIN ||
    value > EQUIPMENT_COMPENSATION_MAX
  ) {
    return "Giá trị bồi thường phải từ 0đ đến 50.000.000đ."
  }
  if (!Number.isInteger(value) || value % EQUIPMENT_COMPENSATION_STEP !== 0) {
    return "Giá trị bồi thường phải là bội số của 1.000đ."
  }
  return null
}

export function formatEquipmentSummary(item: {
  name: string
  quantity?: number | null
  compensationValue?: number | null
}): string {
  const hasQuantity = item.quantity !== undefined && item.quantity !== null
  const hasCompensation =
    item.compensationValue !== undefined && item.compensationValue !== null
  if (!hasQuantity && !hasCompensation) {
    return item.name
  }
  const quantityPart = `${item.name} × ${item.quantity ?? 1}`
  if (!hasCompensation) {
    return quantityPart
  }
  const formattedPrice = new Intl.NumberFormat("vi-VN").format(
    item.compensationValue ?? 0,
  )
  return `${quantityPart} — ${formattedPrice}đ/cái`
}

export function validateEquipmentForm(
  input: {
    mode?: "create" | "edit"
    roomId?: number | string
    name: string
    quantity?: number | string | null
    compensationValue?: number | string | null
    installDate: string
    warrantyExpiry: string
    supplier: string
    status: string
  },
  today: string = getVietnamTodayIsoDate(),
): Partial<Record<EquipmentFormField, string>> {
  const errors: Partial<Record<EquipmentFormField, string>> = {}
  if (input.mode === "create" && input.roomId !== undefined) {
    const roomErr = equipmentRoomIdError(input.roomId)
    if (roomErr) errors.roomId = roomErr
  }
  const nameErr = equipmentNameError(input.name)
  if (nameErr) errors.name = nameErr

  const quantityErr = equipmentQuantityError(input.quantity)
  if (quantityErr) errors.quantity = quantityErr

  const compensationErr = equipmentCompensationValueError(
    input.compensationValue,
  )
  if (compensationErr) errors.compensationValue = compensationErr

  const installErr = installDateError(input.installDate, today)
  if (installErr) errors.installDate = installErr

  const warrantyErr = warrantyExpiryError(
    input.installDate,
    input.warrantyExpiry,
  )
  if (warrantyErr) errors.warrantyExpiry = warrantyErr

  const supplierErr = equipmentSupplierError(input.supplier)
  if (supplierErr) errors.supplier = supplierErr

  const statusErr = equipmentStatusError(input.status)
  if (statusErr) errors.status = statusErr

  return errors
}
