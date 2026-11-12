import { describe, expect, it } from "vitest"
import {
  emailError,
  fullNameError,
  newPasswordError,
  passwordError,
  phoneError,
  registrationError,
} from "@/utils/accountValidation"

// Câu báo lỗi phải giống backend (AccountValidatorsTests.cs).
describe("account validation (A10)", () => {
  const valid = { fullName: "Nguyễn Văn A", email: "a@example.test", password: "matkhau123", phoneNumber: "0901234567" }

  it("accepts a valid registration, with or without phone", () => {
    expect(registrationError(valid)).toBeNull()
    expect(registrationError({ ...valid, phoneNumber: "" })).toBeNull()
    expect(registrationError({ ...valid, phoneNumber: "+84 901 234 567" })).toBeNull()
    expect(registrationError({ ...valid, phoneNumber: "090.123.4567" })).toBeNull()
  })

  it("checks full name", () => {
    expect(fullNameError("  ")).toBe("Họ tên không được để trống.")
    expect(fullNameError("A")).toBe("Họ tên phải từ 2 đến 100 ký tự.")
    expect(fullNameError("a".repeat(101))).toBe("Họ tên phải từ 2 đến 100 ký tự.")
  })

  it.each(["", "no-at-sign", "a@b", "a b@example.test"])("rejects email %j", (email) => {
    expect(emailError(email)).toBe("Email không hợp lệ.")
  })

  it("requires 8+ characters with letters and digits", () => {
    expect(passwordError("abc123")).toBe("Mật khẩu phải có ít nhất 8 ký tự.")
    expect(passwordError("chicochu")).toBe("Mật khẩu phải có cả chữ và số.")
    expect(passwordError("12345678")).toBe("Mật khẩu phải có cả chữ và số.")
    expect(passwordError("a1" + "x".repeat(71))).toBe("Mật khẩu quá dài (tối đa 72 ký tự không dấu).")
    expect(passwordError("a1" + "x".repeat(70))).toBeNull()
  })

  it.each(["123", "090123456", "1901234567", "+8490123456"])("rejects phone %j", (phone) => {
    expect(phoneError(phone)).toBe("Số điện thoại không hợp lệ (ví dụ: 0901234567).")
  })

  it("new password follows the rules and differs from the current one", () => {
    expect(newPasswordError("old-pass-1", "short1")).toBe("Mật khẩu mới phải có ít nhất 8 ký tự.")
    expect(newPasswordError("same-pass-1", "same-pass-1")).toBe("Mật khẩu mới phải khác mật khẩu hiện tại.")
    expect(newPasswordError("old-pass-1", "new-pass-2")).toBeNull()
  })

  it("reports the first error in backend order", () => {
    expect(registrationError({ fullName: "", email: "bad", password: "x", phoneNumber: "1" })).toBe("Họ tên không được để trống.")
    expect(registrationError({ ...valid, email: "bad", password: "x" })).toBe("Email không hợp lệ.")
  })
})
