import { redirect } from "react-router"
import { routes } from "@/constants/routes"
import type { CurrentUser, ManagementRole } from "@/types/user"

export type ManagementPermission = "viewAreas" | "viewReports" | "manageManagers" | "manageUsers" | "createProperty" | "deleteProperty" | "assignManager"

const ROLE_STORAGE_KEY = "troviet-role"
const MANAGEMENT_ROLES: ManagementRole[] = ["ADMIN", "MANAGER"]

export const mockManagementUsers: Record<ManagementRole, CurrentUser> = {
  ADMIN: {
    id: "admin-1",
    name: "Chủ trọ",
    email: "admin@troviet.vn",
    phone: "0900 000 000",
    role: "ADMIN",
  },
  MANAGER: {
    id: "manager-1",
    name: "Nguyễn Văn A",
    email: "nguyenvana@example.com",
    phone: "0901 234 567",
    role: "MANAGER",
    assignedPropertyIds: ["sunrise-residence", "happy-home"],
  },
}

export function getCurrentUser(): CurrentUser {
  const role = window.localStorage.getItem(ROLE_STORAGE_KEY)
  if (role === "ADMIN" || role === "MANAGER") return mockManagementUsers[role]
  return {
    id: "customer-1",
    name: "Ngọc Anh",
    email: "ngocanh@example.com",
    phone: "0909 123 456",
    role: "CUSTOMER",
  }
}

function applyDemoRole(request: Request) {
  const role = new URL(request.url).searchParams.get("as")?.toUpperCase()
  if (role && MANAGEMENT_ROLES.includes(role as ManagementRole)) {
    window.localStorage.setItem("troviet-auth", "true")
    window.localStorage.setItem(ROLE_STORAGE_KEY, role)
  }
}

export function hasPermission(
  user: CurrentUser,
  permission: ManagementPermission,
) {
  if (user.role === "ADMIN") return true
  if (user.role !== "MANAGER") return false
  return (
    [
      "viewAreas",
      "viewReports",
      "manageManagers",
      "manageUsers",
      "deleteProperty",
      "assignManager",
      "createProperty",
    ].includes(permission) === false
  )
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

export function requireManagementRole({ request }: { request: Request }) {
  applyDemoRole(request)
  const user = getCurrentUser()
  if (
    window.localStorage.getItem("troviet-auth") !== "true" ||
    !MANAGEMENT_ROLES.includes(user.role as ManagementRole)
  ) {
    throw redirect(
      `${routes.login}?redirect=${encodeURIComponent(new URL(request.url).pathname)}`,
    )
  }
  return null
}

export function requireManagementPermission(permission: ManagementPermission) {
  return ({ request }: { request: Request }) => {
    requireManagementRole({ request })
    if (!hasPermission(getCurrentUser(), permission))
      throw redirect(routes.managementDashboard)
    return null
  }
}
