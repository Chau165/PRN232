import type { PropertyDto } from "@/types/user"

export interface PropertyManagerAssignmentDto {
  propertyId: number
  propertyName: string
  managerId: number
  managerName: string
  managerEmail: string
  managerIsActive: boolean
}

export interface PropertyManagersDto {
  propertyId: number
  managers: PropertyManagerAssignmentDto[]
}

export interface ManagerPropertiesDto {
  managerId: number
  properties: PropertyDto[]
}
