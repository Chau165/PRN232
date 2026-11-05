export type EquipmentStatus = "Operational"

export type EquipmentDto = {
  equipmentId: number
  roomId: number
  roomCode: string | null
  propertyId: number | null
  propertyName: string | null
  name: string
  installDate: string
  warrantyExpiry: string
  supplier: string
  status: EquipmentStatus | string
}

export type EquipmentWriteRequest = {
  roomId?: number
  name: string
  installDate: string
  warrantyExpiry: string
  supplier: string
  status: EquipmentStatus
}

export type EquipmentFilters = {
  roomId?: number
  propertyId?: number
  status?: string
  search?: string
  page?: number
  pageSize?: number
}
