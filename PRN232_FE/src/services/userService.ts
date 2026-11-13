import { apiRequest } from "@/api/apiClient"
import type { BackendUserDto } from "@/types/auth"
import { MAX_PAGE_SIZE, type PageQuery, type PagedResult } from "@/types/pagination"
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
  // currentPassword is omitted when a Google account sets its first password.
  request: { currentPassword?: string; newPassword: string },
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

/** One page of users (A15). `roleId` filters by role before paging. */
export function getUsersPage(
  accessToken: string,
  query: PageQuery & { roleId?: number } = {},
  signal?: AbortSignal,
) {
  const params = new URLSearchParams()
  if (query.roleId !== undefined) params.set("roleId", String(query.roleId))
  if (query.page !== undefined) params.set("page", String(query.page))
  if (query.pageSize !== undefined) params.set("pageSize", String(query.pageSize))
  const search = params.toString()
  return apiRequest<PagedResult<BackendUserDto>>(`/users${search ? `?${search}` : ""}`, {
    accessToken,
    signal,
  })
}

// Safety stop so a misbehaving backend can never make this loop forever.
const MAX_USER_PAGES = 50

/**
 * Every user (optionally of one role), read page by page with the largest page size.
 * Used by pickers such as the Manager lists, which normally fit in a single request.
 */
export async function getUsers(accessToken: string, roleId?: number) {
  const users: BackendUserDto[] = []
  for (let page = 1; page <= MAX_USER_PAGES; page += 1) {
    const result = await getUsersPage(accessToken, { roleId, page, pageSize: MAX_PAGE_SIZE })
    const items = result?.items ?? []
    users.push(...items)
    if (items.length === 0 || users.length >= (result?.totalCount ?? 0)) break
  }
  return users
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

export function getProperties(accessToken: string, signal?: AbortSignal) {
  return apiRequest<PropertyDto[]>("/properties", {
    accessToken,
    signal,
    cache: "no-store",
  })
}
