export type ManagementRole = "ADMIN" | "MANAGER"
export type AppRole = ManagementRole | "CUSTOMER"

export type CurrentUser = {
  id: string
  name: string
  email: string
  phone: string
  role: AppRole
  roleId?: number
  isActive?: boolean
  assignedPropertyIds?: string[]
}
