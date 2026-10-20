import { apiRequest } from "@/api/apiClient"
import type { BackendUserDto } from "@/types/auth"

export interface RoleDto {
  roleId: number
  roleName: string
}

export interface CreateUserRequestDto {
  fullName: string
  email: string
  password: string
  phoneNumber: string
  roleId: number
}

export interface PropertyDto {
  propertyId: number
  ownerId: number
  ownerName: string
  propertyName: string
  addressText: string
  ward: string
  district: string
  city: string
  roomCount: number
}

export function getCurrentUser(accessToken: string) {
  return apiRequest<BackendUserDto>("/users/me", { accessToken })
}

export function updateCurrentUser(
  accessToken: string,
  request: { fullName: string; phoneNumber: string },
) {
  return apiRequest<BackendUserDto>("/users/me", {
    method: "PUT",
    accessToken,
    body: JSON.stringify(request),
  })
}

export function changeCurrentPassword(
  accessToken: string,
  request: { currentPassword: string; newPassword: string },
) {
  return apiRequest<void>("/users/me/password", {
    method: "PUT",
    accessToken,
    body: JSON.stringify(request),
  })
}

export function getRoles() {
  return apiRequest<RoleDto[]>("/roles")
}

export function getUsers(accessToken: string, roleId?: number) {
  const query = roleId === undefined ? "" : `?roleId=${encodeURIComponent(roleId)}`
  return apiRequest<BackendUserDto[]>(`/users${query}`, { accessToken })
}

export function getUser(accessToken: string, userId: string | number) {
  return apiRequest<BackendUserDto>(`/users/${encodeURIComponent(userId)}`, {
    accessToken,
  })
}

export function createUser(accessToken: string, request: CreateUserRequestDto) {
  return apiRequest<BackendUserDto>("/users", {
    method: "POST",
    accessToken,
    body: JSON.stringify(request),
  })
}

export function updateUserRole(
  accessToken: string,
  userId: string | number,
  roleId: number,
) {
  return apiRequest<BackendUserDto>(`/users/${encodeURIComponent(userId)}/role`, {
    method: "PUT",
    accessToken,
    body: JSON.stringify({ roleId }),
  })
}

export function updateUserStatus(
  accessToken: string,
  userId: string | number,
  isActive: boolean,
) {
  return apiRequest<BackendUserDto>(`/users/${encodeURIComponent(userId)}/status`, {
    method: "PUT",
    accessToken,
    body: JSON.stringify({ isActive }),
  })
}

export function getPropertiesOwnedBy(accessToken: string, ownerId: string | number) {
  return apiRequest<PropertyDto[]>(
    `/properties?ownerId=${encodeURIComponent(ownerId)}`,
    { accessToken },
  )
}
