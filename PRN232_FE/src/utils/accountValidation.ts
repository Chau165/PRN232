// Quy tắc dữ liệu tài khoản (A10) — giống hệt backend:
// PRN232_BE/PRN232.Application/Validation/AccountValidators.cs. Sửa một bên thì sửa cả bên kia,
// kể cả câu báo lỗi. Backend vẫn là nơi kiểm tra cuối cùng; FE kiểm tra trước để báo lỗi ngay.

export const FULL_NAME_MIN_LENGTH = 2
export const FULL_NAME_MAX_LENGTH = 100
export const EMAIL_MAX_LENGTH = 254
export const PASSWORD_MIN_LENGTH = 8
// BCrypt chỉ dùng 72 byte đầu của mật khẩu.
export const PASSWORD_MAX_BYTES = 72

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/
const VIETNAM_PHONE_PATTERN = /^(0|\+84)\d{9}$/
const PHONE_SEPARATORS = /[\s.\-()]/g
const LETTER = /\p{L}/u
const DIGIT = /\d/

export function normalizePhone(phone: string): string {
  return phone.replace(PHONE_SEPARATORS, "")
}

export function fullNameError(fullName: string): string | null {
  const trimmed = fullName.trim()
  if (!trimmed) return "Họ tên không được để trống."
  if (trimmed.length < FULL_NAME_MIN_LENGTH || trimmed.length > FULL_NAME_MAX_LENGTH) {
    return `Họ tên phải từ ${FULL_NAME_MIN_LENGTH} đến ${FULL_NAME_MAX_LENGTH} ký tự.`
  }
  return null
}

export function emailError(email: string): string | null {
  const trimmed = email.trim()
  if (!trimmed || trimmed.length > EMAIL_MAX_LENGTH || !EMAIL_PATTERN.test(trimmed)) {
    return "Email không hợp lệ."
  }
  return null
}

export function phoneError(phone: string): string | null {
  if (!phone.trim()) return null
  return VIETNAM_PHONE_PATTERN.test(normalizePhone(phone))
    ? null
    : "Số điện thoại không hợp lệ (ví dụ: 0901234567)."
}

// label: "Mật khẩu" hoặc "Mật khẩu mới".
export function passwordError(password: string, label = "Mật khẩu"): string | null {
  if (password.length < PASSWORD_MIN_LENGTH) {
    return `${label} phải có ít nhất ${PASSWORD_MIN_LENGTH} ký tự.`
  }
  if (new TextEncoder().encode(password).length > PASSWORD_MAX_BYTES) {
    return `${label} quá dài (tối đa ${PASSWORD_MAX_BYTES} ký tự không dấu).`
  }
  if (!LETTER.test(password) || !DIGIT.test(password)) {
    return `${label} phải có cả chữ và số.`
  }
  return null
}

export function newPasswordError(currentPassword: string, newPassword: string): string | null {
  return (
    passwordError(newPassword, "Mật khẩu mới") ??
    (newPassword === currentPassword ? "Mật khẩu mới phải khác mật khẩu hiện tại." : null)
  )
}

// Thứ tự giống validator backend: họ tên → email → mật khẩu → số điện thoại.
export function registrationError(input: {
  fullName: string
  email: string
  password: string
  phoneNumber: string
}): string | null {
  return (
    fullNameError(input.fullName) ??
    emailError(input.email) ??
    passwordError(input.password) ??
    phoneError(input.phoneNumber)
  )
}

export const PASSWORD_HINT = `Ít nhất ${PASSWORD_MIN_LENGTH} ký tự, gồm cả chữ và số.`
