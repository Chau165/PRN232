// Quy tắc validate thiết bị (C3 — docs/rebuild/API-CHANGES.md).

export const EQUIPMENT_NAME_MAX_LENGTH = 200
export const EQUIPMENT_SUPPLIER_MAX_LENGTH = 200

export type EquipmentFormField =
  | "roomId"
  | "name"
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

export function validateEquipmentForm(
  input: {
    mode?: "create" | "edit"
    roomId?: number | string
    name: string
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
