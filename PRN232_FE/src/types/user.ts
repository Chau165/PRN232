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

export interface MaintenanceRoomDto {
  roomId: number
  propertyId: number
  propertyName: string
  roomCode: string
}
