import { getCurrentUserSnapshot } from "@/auth/currentUserStore"
import type { CurrentUser } from "@/types/user"

export type ManagementPermission =
  | "viewAreas"
  | "viewReports"
  | "manageManagers"
  | "manageUsers"
  | "createProperty"
  | "deleteProperty"
  | "assignManager"

export function getCurrentUser(): CurrentUser {
  const user = getCurrentUserSnapshot()
  if (!user) throw new Error("Authenticated user is not available.")
  return user
}

export function hasPermission(
  user: CurrentUser,
  permission: ManagementPermission,
) {
  const adminPermissions: ManagementPermission[] = [
    "viewAreas",
    "viewReports",
    "manageManagers",
    "manageUsers",
  ]
  if (user.role === "ADMIN") return true
  if (user.role !== "MANAGER") return false
  return !adminPermissions.includes(permission)
}

export function canAccessProperty(user: CurrentUser, propertyId: string) {
  return (
    user.role === "ADMIN" ||
    user.assignedPropertyIds?.includes(propertyId) === true
  )
}

export function getAccessiblePropertyIds(
  user: CurrentUser,
  propertyIds: string[],
) {
  return user.role === "ADMIN"
    ? propertyIds
    : propertyIds.filter((id) => user.assignedPropertyIds?.includes(id))
}
