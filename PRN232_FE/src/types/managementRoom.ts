export type ManagementRoomStatus = "Available" | "Reserved" | "Occupied" | "Maintenance"

export type ManagementRoomDto = {
  roomId: number
  propertyId: number
  propertyName: string
  roomTypeId: number
  roomTypeName: string
  roomTypePrice: number
  roomCode: string
  areaSqm: number
  status: ManagementRoomStatus | string
  primaryImageUrl?: string | null
}

export type RoomTypeDto = {
  roomTypeId: number
  typeName: string
  price: number
}

export type CreateRoomTypeRequest = {
  typeName: string
  price: number
}

export type RoomImageDto = {
  roomImageId: number
  roomId: number
  imageUrl: string
  isPrimary: boolean
}

export type CreateRoomRequest = {
  propertyId: number
  roomTypeId: number
  roomCode: string
  areaSqm: number
}

export type UpdateRoomRequest = {
  roomTypeId: number
  roomCode: string
  areaSqm: number
}
