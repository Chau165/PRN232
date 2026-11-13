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
