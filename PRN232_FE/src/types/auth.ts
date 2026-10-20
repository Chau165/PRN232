import type { CurrentUser } from "@/types/user"

export type PasswordInputProps = {
  value: string
  onChange: (value: string) => void
  invalid?: boolean
}

export interface BackendUserDto {
  userId: number | string
  fullName: string
  email: string
  phoneNumber: string
  roleId: number
  roleName: string
  isActive?: boolean
}

export interface AuthResponseDto {
  accessToken: string
  expiresAtUtc: string
  user: BackendUserDto
}

export interface LoginRequestDto {
  email: string
  password: string
}

export interface RegisterRequestDto extends LoginRequestDto {
  fullName: string
  phoneNumber: string
}

export function mapBackendUser(dto: BackendUserDto): CurrentUser {
  if (typeof dto.roleName !== "string" || !dto.roleName.trim()) {
    throw new Error("Backend không trả về role hợp lệ cho tài khoản.")
  }
  const role = dto.roleName.trim().toUpperCase()
  if (role !== "ADMIN" && role !== "MANAGER" && role !== "CUSTOMER") {
    throw new Error("Tài khoản có role không được hỗ trợ.")
  }

  return {
    id: String(dto.userId),
    name: dto.fullName,
    email: dto.email,
    phone: dto.phoneNumber,
    role,
    roleId: dto.roleId,
    isActive: dto.isActive,
  }
}
