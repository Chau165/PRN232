// Quy tắc validate khu trọ và bảng giá dịch vụ (C3 — docs/rebuild/API-CHANGES.md).

export const PROPERTY_NAME_MIN_LENGTH = 3
export const PROPERTY_NAME_MAX_LENGTH = 150
export const ADDRESS_TEXT_MIN_LENGTH = 5
export const ADDRESS_TEXT_MAX_LENGTH = 255
export const PROPERTY_DESCRIPTION_MAX_LENGTH = 2_000

export type PropertyFormField =
  | "propertyName"
  | "addressText"
  | "description"
  | "provinceCode"
  | "wardCode"
  | "primaryImage"

export type ServiceRateFormField =
  | "serviceId"
  | "billing"
  | "unitCode"
  | "billingBasis"
  | "price"
  | "effectiveFrom"
  | "effectiveTo"

export function propertyNameError(propertyName: string): string | null {
  const trimmed = propertyName.trim()
  if (!trimmed) {
    return "Tên khu trọ không được để trống."
  }
  if (
    trimmed.length < PROPERTY_NAME_MIN_LENGTH ||
    trimmed.length > PROPERTY_NAME_MAX_LENGTH
  ) {
    return `Tên khu trọ phải từ ${PROPERTY_NAME_MIN_LENGTH} đến ${PROPERTY_NAME_MAX_LENGTH} ký tự.`
  }
  return null
}

export function addressTextError(addressText: string): string | null {
  const trimmed = addressText.trim()
  if (!trimmed) {
    return "Số nhà, tên đường không được để trống."
  }
  if (
    trimmed.length < ADDRESS_TEXT_MIN_LENGTH ||
    trimmed.length > ADDRESS_TEXT_MAX_LENGTH
  ) {
    return `Số nhà, tên đường phải từ ${ADDRESS_TEXT_MIN_LENGTH} đến ${ADDRESS_TEXT_MAX_LENGTH} ký tự.`
  }
  return null
}

export function propertyDescriptionError(description: string): string | null {
  const trimmed = description.trim()
  if (trimmed.length > PROPERTY_DESCRIPTION_MAX_LENGTH) {
    return `Mô tả tối đa ${PROPERTY_DESCRIPTION_MAX_LENGTH} ký tự.`
  }
  return null
}

export function serviceIdError(serviceId: number | string): string | null {
  if (typeof serviceId === "string" && serviceId.trim() === "") {
    return "Vui lòng chọn dịch vụ."
  }
  const id = typeof serviceId === "number" ? serviceId : Number(serviceId)
  if (!Number.isInteger(id) || id < 1) {
    return "Vui lòng chọn dịch vụ."
  }
  return null
}

export function parseServiceRatePriceInput(raw: string): number {
  const trimmed = raw.trim()
  if (!trimmed) return Number.NaN
  // Hỗ trợ dấu chấm phân cách hàng nghìn kiểu Việt Nam (ví dụ "18.000" -> "18000"),
  // nhưng giữ nguyên số thập phân như "10.5" để báo lỗi "Đơn giá phải là số đồng nguyên.".
  const normalized = /^\d{1,3}(\.\d{3})+$/.test(trimmed)
    ? trimmed.replace(/\./g, "")
    : trimmed
  return Number(normalized)
}

export function serviceRatePriceError(price: number | string): string | null {
  if (typeof price === "string" && price.trim() === "") {
    return "Đơn giá phải lớn hơn 0."
  }
  const value =
    typeof price === "number" ? price : parseServiceRatePriceInput(price)
  if (!Number.isFinite(value) || value <= 0) {
    return "Đơn giá phải lớn hơn 0."
  }
  if (!Number.isSafeInteger(value)) {
    return "Đơn giá phải là số đồng nguyên."
  }
  return null
}

export function validatePropertyForm(input: {
  propertyName: string
  addressText: string
  description: string
  validateAddressText?: boolean
}): Partial<Record<PropertyFormField, string>> {
  const errors: Partial<Record<PropertyFormField, string>> = {}
  const nameErr = propertyNameError(input.propertyName)
  if (nameErr) errors.propertyName = nameErr

  if (input.validateAddressText ?? true) {
    const addrErr = addressTextError(input.addressText)
    if (addrErr) errors.addressText = addrErr
  }

  const descErr = propertyDescriptionError(input.description)
  if (descErr) errors.description = descErr

  return errors
}
