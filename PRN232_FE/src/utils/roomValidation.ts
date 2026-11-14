// Quy tắc validate phòng và giá thuê theo khu trọ (C2 — docs/rebuild/API-CHANGES.md).

export const ROOM_CODE_MAX_LENGTH = 20
export const ROOM_AREA_MIN = 8
export const ROOM_AREA_MAX = 100
export const ROOM_AREA_STEP = 0.1

export const MONTHLY_RENT_MIN = 500_000
export const MONTHLY_RENT_MAX = 50_000_000
export const MONTHLY_RENT_STEP = 1_000

const ROOM_CODE_PATTERN = /^[A-Za-z0-9.-]+$/

export type RoomFormField = "propertyId" | "roomTypeId" | "roomCode" | "areaSqm"
export type PropertyRoomTypeFormField = "roomTypeId" | "monthlyRent"

export function propertyIdError(propertyId: number | string): string | null {
  if (typeof propertyId === "string" && propertyId.trim() === "") {
    return "Vui lòng chọn khu trọ."
  }
  const id = typeof propertyId === "number" ? propertyId : Number(propertyId)
  if (!Number.isInteger(id) || id < 1) {
    return "Vui lòng chọn khu trọ."
  }
  return null
}

export function roomTypeIdError(roomTypeId: number | string): string | null {
  if (typeof roomTypeId === "string" && roomTypeId.trim() === "") {
    return "Vui lòng chọn loại phòng."
  }
  const id = typeof roomTypeId === "number" ? roomTypeId : Number(roomTypeId)
  if (!Number.isInteger(id) || id < 1) {
    return "Vui lòng chọn loại phòng."
  }
  return null
}

export function roomCodeError(roomCode: string): string | null {
  const trimmed = roomCode.trim()
  if (!trimmed) {
    return "Mã phòng không được để trống."
  }
  if (trimmed.length > ROOM_CODE_MAX_LENGTH) {
    return `Mã phòng tối đa ${ROOM_CODE_MAX_LENGTH} ký tự.`
  }
  if (!ROOM_CODE_PATTERN.test(trimmed)) {
    return "Mã phòng chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc dấu chấm (ví dụ: A-101)."
  }
  return null
}

export function areaSqmError(areaSqm: number | string): string | null {
  if (typeof areaSqm === "string" && areaSqm.trim() === "") {
    return "Diện tích phòng phải từ 8 đến 100 m²."
  }
  const value = typeof areaSqm === "number" ? areaSqm : Number(areaSqm)
  if (!Number.isFinite(value) || value < ROOM_AREA_MIN || value > ROOM_AREA_MAX) {
    return "Diện tích phòng phải từ 8 đến 100 m²."
  }
  if (Math.abs(value * 10 - Math.round(value * 10)) > 1e-7) {
    return "Diện tích chỉ được có tối đa 1 chữ số thập phân."
  }
  return null
}

export function monthlyRentError(monthlyRent: number | string): string | null {
  if (typeof monthlyRent === "string" && monthlyRent.trim() === "") {
    return "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng."
  }
  const value = typeof monthlyRent === "number" ? monthlyRent : Number(monthlyRent)
  if (!Number.isFinite(value) || value < MONTHLY_RENT_MIN || value > MONTHLY_RENT_MAX) {
    return "Giá thuê phải từ 500.000đ đến 50.000.000đ mỗi tháng."
  }
  if (!Number.isInteger(value) || value % MONTHLY_RENT_STEP !== 0) {
    return "Giá thuê phải là bội số của 1.000đ."
  }
  return null
}

export function validateRoomForm(input: {
  mode: "create" | "edit"
  propertyId?: number | string
  roomTypeId: number | string
  roomCode: string
  areaSqm: number | string
}): Partial<Record<RoomFormField, string>> {
  const errors: Partial<Record<RoomFormField, string>> = {}
  if (input.mode === "create") {
    const propErr = propertyIdError(input.propertyId ?? "")
    if (propErr) errors.propertyId = propErr
  }
  const typeErr = roomTypeIdError(input.roomTypeId)
  if (typeErr) errors.roomTypeId = typeErr

  const codeErr = roomCodeError(input.roomCode)
  if (codeErr) errors.roomCode = codeErr

  const areaErr = areaSqmError(input.areaSqm)
  if (areaErr) errors.areaSqm = areaErr

  return errors
}

export function validatePropertyRoomTypeForm(input: {
  mode?: "create" | "edit"
  roomTypeId?: number | string
  monthlyRent: number | string
}): Partial<Record<PropertyRoomTypeFormField, string>> {
  const errors: Partial<Record<PropertyRoomTypeFormField, string>> = {}
  if ((input.mode ?? "create") === "create") {
    const typeErr = roomTypeIdError(input.roomTypeId ?? "")
    if (typeErr) errors.roomTypeId = typeErr
  }
  const rentErr = monthlyRentError(input.monthlyRent)
  if (rentErr) errors.monthlyRent = rentErr

  return errors
}

export function splitValidationErrors<Field extends string>(
  validationErrors: Record<string, string[]> | undefined,
  knownFields: readonly Field[],
  fallbackMessage: string,
): { fieldErrors: Partial<Record<Field, string>>; generalError: string } {
  if (!validationErrors) {
    return { fieldErrors: {}, generalError: fallbackMessage }
  }

  const fieldErrors: Partial<Record<Field, string>> = {}
  const unmappedMessages: string[] = []
  const fieldLookup = new Map<string, Field>(
    knownFields.map((field) => [field.toLowerCase(), field]),
  )

  for (const [rawKey, rawMessages] of Object.entries(validationErrors)) {
    const messages = rawMessages.map((msg) => msg.trim()).filter(Boolean)
    if (messages.length === 0) continue
    const lastSegment = rawKey.split(".").pop() ?? rawKey
    const matchedField =
      fieldLookup.get(rawKey.toLowerCase()) ??
      fieldLookup.get(lastSegment.toLowerCase())
    if (matchedField) {
      fieldErrors[matchedField] = messages.join(" ")
    } else {
      unmappedMessages.push(...messages)
    }
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
