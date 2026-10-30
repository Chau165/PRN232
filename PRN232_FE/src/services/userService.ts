import { apiRequest } from "@/api/apiClient"
import type { BackendUserDto } from "@/types/auth"
import type {
  CreateUserRequestDto,
  MaintenanceRoomDto,
  PropertyDto,
  RoleDto,
} from "@/types/user"

export function getMaintenanceRooms(accessToken: string, signal?: AbortSignal) {
  return apiRequest<MaintenanceRoomDto[]>("/rooms", { accessToken, signal, cache: "no-store" })
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
    json: request,
  })
}

export function changeCurrentPassword(
  accessToken: string,
  request: { currentPassword: string; newPassword: string },
) {
  return apiRequest<void>("/users/me/password", {
    method: "PUT",
    accessToken,
    json: request,
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
    json: request,
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
    json: { roleId },
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
    json: { isActive },
  })
}

export function getPropertiesOwnedBy(accessToken: string, ownerId: string | number) {
  return apiRequest<PropertyDto[]>(
    `/properties?ownerId=${encodeURIComponent(ownerId)}`,
    { accessToken },
  )
}
